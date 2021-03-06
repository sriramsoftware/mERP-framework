﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Feng.Windows.Utils;

namespace Feng.Windows.Forms
{
    public partial class MsReportForm : Form
    {
        public MsReportForm()
        {
            InitializeComponent();
        }

        private void MsReportForm_Load(object sender, EventArgs e)
        {
            this.reportViewer1.RefreshReport();
        }
        public MsReportForm(string reportInfoName)
            : this()
        {
            m_reportInfo = ADInfoBll.Instance.GetReportInfo(reportInfoName);
            if (m_reportInfo == null)
            {
                throw new ArgumentException("不存在名为" + reportInfoName + "的ReportInfo!");
            }

            string reportFile = ReportHelper.CreateMsReportFile(m_reportInfo.ReportDocument);
            this.reportViewer1.LocalReport.ReportPath = reportFile;
            m_dataSet = ReportHelper.CreateDataset(m_reportInfo.DatasetName);
            foreach (DataTable dt in m_dataSet.Tables)
            {
                this.reportViewer1.LocalReport.DataSources.Add(
                    new Microsoft.Reporting.WinForms.ReportDataSource(m_dataSet.DataSetName + "_" + dt.TableName, dt));
            }
            m_reportDataInfos = ADInfoBll.Instance.GetReportDataInfo(m_reportInfo.Name);

            foreach (ReportDataInfo reportDataInfo in m_reportDataInfos)
            {
                if (string.IsNullOrEmpty(reportDataInfo.SearchManagerClassName))
                {
                    throw new ArgumentException("ReportDataInfo of " + reportDataInfo.Name + " 's SearchManagerClassName must not be null!");
                }

                ISearchManager sm = ServiceProvider.GetService<IManagerFactory>().GenerateSearchManager(reportDataInfo.SearchManagerClassName, reportDataInfo.SearchManagerClassParams);

                sm.EnablePage = false;

                m_sms.Add(sm);
            }

            this.FormClosed += new FormClosedEventHandler(MsReportForm_FormClosed);
        }

        void MsReportForm_FormClosed(object sender, FormClosedEventArgs e)
        {
        }

        private DataSet m_dataSet;
        private ReportInfo m_reportInfo;
        private IList<ReportDataInfo> m_reportDataInfos;
        private IList<ISearchManager> m_sms = new List<ISearchManager>();

        /// <summary>
        /// 以entities为数据打印（例如滞箱费减免联系单）
        /// </summary>
        /// <param name="entities"></param>
        public void FillDataSet(object[] entities)
        {
            ISearchExpression se = null;
            foreach (object entity in entities)
            {
                if (se == null)
                {
                    se = SearchExpression.Parse(EntityHelper.ReplaceEntity(m_reportInfo.SearchExpression, entity));
                }
                else
                {
                    se = SearchExpression.Or(se, SearchExpression.Parse(EntityHelper.ReplaceEntity(m_reportInfo.SearchExpression, entity)));
                }
            }
            for (int i = 0; i < m_reportDataInfos.Count; ++i)
            {
                object data = m_sms[i].GetData(se, null);
                System.Collections.IEnumerable dataList = data as System.Collections.IEnumerable;
                if (dataList == null)
                {
                    dataList = (data as System.Data.DataTable).DefaultView;
                }

                FillDataSet(i, dataList);
            }
        }

        /// <summary>
        /// 以entity为数据打印（例如凭证）
        /// </summary>
        /// <param name="entity"></param>
        public void FillDataSet(object entity)
        {
            ISearchExpression se = SearchExpression.Parse(EntityHelper.ReplaceEntity(m_reportInfo.SearchExpression, entity));
            for (int i = 0; i < m_reportDataInfos.Count; ++i)
            {
                System.Collections.IEnumerable dataList = m_sms[i].GetData(se, null);

                FillDataSet(i, dataList);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reportIdx"></param>
        /// <param name="data"></param>
        public void FillDataSet(int reportIdx, IEnumerable dataList)
        {
            if (reportIdx < 0 || reportIdx >= m_reportDataInfos.Count)
            {
                throw new ArgumentException("reportIdx");
            }
            string s = m_reportDataInfos[reportIdx].DatasetTableName;

            if (!m_dataSet.Tables.Contains(s))
            {
                throw new ArgumentException("报表DataSet中未包含名为" + s + "的DataTable！");
            }
            System.Data.DataTable dt = m_dataSet.Tables[s];
            dt.Rows.Clear();
            GenerateReportData.Generate(dt, dataList, m_reportDataInfos[reportIdx].GridName);
        }
    }
}
