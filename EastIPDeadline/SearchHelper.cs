using System.Collections;
using System.Data;
using System.Linq;
using DataSyncTool.DBUtility;
using DevExpress.XtraPrinting.Native;

namespace EastIPDeadline
{
    public class SearchHelper
    {
        private DataTable _dtAlertMeta;
        private DataTable _dtHeaderMapping;
        private DataTable _dtPatentExInfo;
        private readonly string _sSqlAlertMeta = "select * from ALERTMETA";

        private readonly string _sSqlFCaseEntRel =
            @"select fc.ourno OURNO,fc.role ROLE,cd.clientname NAME from fcase_ent_rel fc,clientdb cd where fc.eid = cd.clientid(+)";

        private readonly string _sSqlHeaderMapping = "select * from HEADERMAPPINGTB";

        private readonly string _sSqlHKCaseInfo =
            @"select hk_app_ref OURNO,client CLIENTNAME,applicant APPNAME from ex_hkcase";

        private readonly string _sSqlPatentExInfo =
            @"select pc.ourno OURNO,cd.clientname CLIENTNAME,(app1.clientname||';'||app2.clientname||';'||app3.clientname||';'||app4.clientname||';'||app5.clientname) APPNAME
from patentcase pc, clientdb cd,clientdb app1,clientdb app2,clientdb app3,clientdb app4,clientdb app5
where pc.client = cd.clientid (+) and pc.appl_code1 = app1.clientid(+) and pc.appl_code2 = app2.clientid(+) and pc.appl_code3 = app3.clientid(+) and pc.appl_code4 = app4.clientid(+) and pc.appl_code5 = app5.clientid(+)";

        public SearchHelper()
        {
            InitSourceTables();
        }

        private void InitSourceTables()
        {
            _dtAlertMeta = DbHelperOra.Query(_sSqlAlertMeta).Tables[0];
            _dtHeaderMapping = DbHelperOra.Query(_sSqlHeaderMapping).Tables[0];
            _dtPatentExInfo = DbHelperOra.Query(_sSqlPatentExInfo).Tables[0];
            _dtPatentExInfo.Rows.Cast<DataRow>().ForEach(dr => dr["APPNAME"] = dr["APPNAME"].ToString().TrimEnd(';'));
            _dtPatentExInfo.Merge(DbHelperOra.Query(_sSqlHKCaseInfo).Tables[0]);

            var dtFCaseEntRel = DbHelperOra.Query(_sSqlFCaseEntRel).Tables[0];
            var dtFCase = _dtPatentExInfo.Clone();
            dtFCaseEntRel.Rows.Cast<DataRow>().GroupBy(dr => dr["OURNO"]).ForEach(g =>
            {
                var dr = dtFCase.NewRow();
                dr["OURNO"] = g.Key.ToString();
                dr["CLIENTNAME"] = string.Join(";",
                    g.Where(l => l["ROLE"].ToString() == "CLI" || l["ROLE"].ToString() == "APPCLI")
                        .Select(l => l["NAME"].ToString()));
                dr["APPNAME"] = string.Join(";",
                    g.Where(l => l["ROLE"].ToString() == "APP" || l["ROLE"].ToString() == "APPCLI")
                        .Select(l => l["NAME"].ToString()));
                dtFCase.Rows.Add(dr);
            });

            _dtPatentExInfo.Merge(dtFCase);
        }

        private DataTable GeneralSqlQuery(string sSelect, string sFrom, string sWhere, string sOrderBy)
        {
            var sSqlSelect = string.Join(",",
                sSelect.Split(',')
                    .Select(
                        s =>
                        {
                            var drs = _dtHeaderMapping.Select($"ID = '{s.Trim()}'");
                            var sRealSelect = drs.Length > 0 ? drs[0]["EXPRESSION"].ToString() : s;
                            if (drs.Length > 0 && drs[0]["FORMATTER"].ToString().ToLower().Contains("toint") &&
                                drs[0]["ID"].ToString() != "oa_forward_daysleft")
                                sRealSelect = $"round({sRealSelect})";

                            return sRealSelect;
                        }));
            var sSql = $"select {sSqlSelect} from {sFrom} where {sWhere} order by {sOrderBy} asc";
            var dt = DbHelperOra.Query(sSql).Tables[0];
            SetColumnName(sSelect, dt);
            SetPatentExInfo(dt);
            return dt;
        }

        private void SetColumnName(string sSelect, DataTable dt)
        {
            for (var i = 0; i < sSelect.Split(',').Length; i++)
            {
                var sSelectValue = sSelect.Split(',')[i];
                var drs = _dtHeaderMapping.Select($"ID = '{sSelectValue.Trim()}'");
                if (drs.Length > 0 && !string.IsNullOrEmpty(drs[0]["HEADING"].ToString()) &&
                    !dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).Contains(drs[0]["HEADING"].ToString()))
                    dt.Columns[i].ColumnName = drs[0]["HEADING"].ToString();
            }
        }

        private void SetPatentExInfo(DataTable dt)
        {
            var dcClient = dt.Columns.Add("客户名称");
            var dcApp = dt.Columns.Add("申请人名称");

            dt.Rows.Cast<DataRow>().ToList().ForEach(dr =>
            {
                var dcOurNo = dt.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.Contains("卷号"));
                var drEx =
                    _dtPatentExInfo.Select($"ourno = '{(dcOurNo != null ? dr[dcOurNo].ToString() : dr[0].ToString())}'")
                        [0];
                dr["客户名称"] = drEx[1].ToString();
                dr["申请人名称"] = drEx[2].ToString();
            });
            dcClient.SetOrdinal(1);
            dcApp.SetOrdinal(2);
        }

        public Hashtable GeneralSearchResults()
        {
            var htTable = new Hashtable();
            foreach (var dataRow in _dtAlertMeta.Rows.Cast<DataRow>())
            {
                var dt = GeneralSqlQuery(dataRow["ALERT_SELECT"].ToString(), dataRow["ALERT_FROM"].ToString(),
                    dataRow["ALERT_WHERE"].ToString(), dataRow["ALERT_ORDERBY"].ToString());
                dt.TableName = dataRow["TITLE"].ToString();
                htTable.Add(dataRow, dt);
            }
            //SetColumnName(htTable);
            htTable.Values.Cast<DataTable>().ForEach(RemoveUnuseColumns);
            return htTable;
        }

        private void RemoveUnuseColumns(DataTable dt)
        {
            dt.Columns.Remove(dt.Columns[0]);
            var columnAgent = dt.Columns.Cast<DataColumn>().FirstOrDefault(dc => dc.ColumnName.ToLower().Contains("agent"));
            if (columnAgent != null)
                dt.Columns.Remove(columnAgent);
        }

        //}
        //    }
        //        }
        //                ((DataTable)ht[key]).Columns[i + 1].ColumnName = listColumnNames[i];
        //            else
        //                ((DataTable)ht[key]).Columns[i + 1].ColumnName = listColumnNames[i] + (((DataTable)ht[key]).Columns.Cast<DataColumn>().Count(c => c.ColumnName == listColumnNames[i]) + 1);
        //            if (((DataTable)ht[key]).Columns.Cast<DataColumn>().Any(c => c.ColumnName == listColumnNames[i]))
        //        {
        //        for (int i = 0; i < listColumnNames.Count; i++)
        //        var listColumnNames = key["TH"].ToString().Replace("<th>", "").Replace("</th>", "").Split('\n').Where(s => !string.IsNullOrEmpty(s)).ToList();
        //    {
        //    foreach (DataRow key in ht.Keys)
        //{

        //public void SetColumnName(Hashtable ht)
    }
}