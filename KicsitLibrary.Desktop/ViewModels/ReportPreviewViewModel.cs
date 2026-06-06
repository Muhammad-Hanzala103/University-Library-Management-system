using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using KicsitLibrary.Reports.Models;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class ReportPreviewViewModel : ObservableObject
    {
        [ObservableProperty] private string _reportTitle = "Report Preview";
        [ObservableProperty] private string _previewMessage =
            "Select a report to preview data.";
        [ObservableProperty] private DataView? _previewRows;
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private bool _isEmpty = true;
        [ObservableProperty] private ObservableCollection<KeyValuePair<string, string>>
            _summaryItems = [];

        public ReportResult? CurrentResult { get; private set; }

        public void SetResult(ReportResult result)
        {
            CurrentResult = result;
            ReportTitle = result.ReportTitle;
            TotalCount = result.TotalCount;
            IsEmpty = result.Rows.Count == 0;
            PreviewMessage = IsEmpty
                ? "No records matched the selected filters."
                : $"{result.TotalCount} record(s) loaded.";
            SummaryItems = new ObservableCollection<KeyValuePair<string, string>>(
                result.SummaryItems);

            var table = new DataTable();
            foreach (var column in result.Columns)
            {
                table.Columns.Add(column.Header, typeof(object));
            }

            foreach (var reportRow in result.Rows)
            {
                var row = table.NewRow();
                for (var index = 0; index < result.Columns.Count; index++)
                {
                    row[index] =
                        reportRow[result.Columns[index].Key] ?? DBNull.Value;
                }
                table.Rows.Add(row);
            }

            PreviewRows = table.DefaultView;
        }

        public void Clear()
        {
            CurrentResult = null;
            ReportTitle = "Report Preview";
            PreviewMessage = "Select a report to preview data.";
            PreviewRows = null;
            TotalCount = 0;
            IsEmpty = true;
            SummaryItems.Clear();
        }
    }
}
