using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class SettingsDetailsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string key = string.Empty;

        [ObservableProperty]
        private string displayName = string.Empty;

        [ObservableProperty]
        private string category = string.Empty;

        [ObservableProperty]
        private string? description;

        [ObservableProperty]
        private string value = string.Empty;

        [ObservableProperty]
        private string valueMasked = string.Empty;

        [ObservableProperty]
        private string defaultValue = string.Empty;

        [ObservableProperty]
        private string dataType = "String";

        [ObservableProperty]
        private bool isSensitive;

        [ObservableProperty]
        private bool requiresRestart;

        [ObservableProperty]
        private DateTime? updatedAt;

        [ObservableProperty]
        private string? updatedBy;

        public void LoadSetting(SettingsItemView setting)
        {
            Key = setting.Key;
            DisplayName = setting.DisplayName;
            Category = setting.Category;
            Description = setting.Description;
            Value = setting.Value;
            ValueMasked = setting.MaskedValue;
            DefaultValue = setting.DefaultValue;
            DataType = setting.DataType;
            IsSensitive = setting.IsSensitive;
            RequiresRestart = setting.RequiresRestart;
            UpdatedAt = setting.UpdatedAt;
            UpdatedBy = setting.UpdatedBy;
        }

        [RelayCommand]
        private void Close()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? CloseRequested;
    }
}
