using System;
using System.Data;
using System.Threading;
using DevExpress.Data;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraLayout;

namespace EastIPDeadline
{
    public partial class XFrmMain : XtraForm
    {
        public XFrmMain()
        {
            InitializeComponent();
        }

        private void xsbSearch_Click(object sender, EventArgs e)
        {
            GenerateGrid();
        }

        private void GenerateGrid()
        {
            xsbSearch.Enabled = false;
            xsbSearch.Text = "拉取数据大约需要几分钟的时间，请稍后...";
            tabbedControlGroup.TabPages.Clear();
            var thread = new Thread(() =>
            {
                try
                {
                    var htResult = new SearchHelper().GeneralSearchResults();
                    foreach (DataTable dtResult in htResult.Values)
                        Invoke(new Action(() =>
                        {
                            var gc = new GridControl();
                            gc.DataSource = dtResult;
                            gc.Load += Gc_Load;
                            tabbedControlGroup.AddTabPage(dtResult.TableName)
                                .Add(new LayoutControlItem
                                {
                                    Control = gc,
                                    Text = dtResult.TableName,
                                    TextLocation = Locations.Top,
                                    TextVisible = false
                                });
                        }));
                    Invoke(new Action(() =>
                    {
                        xsbSearch.Text = "拉取数据";
                        xsbSearch.Enabled = true;
                    }));
                }
                catch (Exception e)
                {
                    XtraMessageBox.Show(e.ToString());
                }
            });
            thread.Start();
        }

        private void Gc_Load(object sender, EventArgs e)
        {
            var gv = (GridView) ((GridControl) sender).MainView;
            gv.OptionsBehavior.Editable = false;
            gv.OptionsView.ShowFooter = true;
            gv.OptionsFind.AllowFindPanel = true;
            gv.OptionsFind.AlwaysVisible = true;
            gv.OptionsView.ShowAutoFilterRow = true;
            gv.Columns[0].Summary.Add(SummaryItemType.Count);
        }
    }
}