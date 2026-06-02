using System;

namespace KicsitLibrary.Core.Interfaces
{
    public interface INavigationService
    {
        string CurrentViewName { get; }
        event Action<string>? NavigationChanged;
        void NavigateTo(string viewName);
    }
}
