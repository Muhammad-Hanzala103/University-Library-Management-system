using System;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.Services
{
    public class NavigationService : INavigationService
    {
        public string CurrentViewName { get; private set; } = "Dashboard";

        public event Action<string>? NavigationChanged;

        public void NavigateTo(string viewName)
        {
            if (CurrentViewName != viewName)
            {
                CurrentViewName = viewName;
                NavigationChanged?.Invoke(viewName);
            }
        }
    }
}
