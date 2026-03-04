using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Spectro.Core.Interfaces
{
    public enum NavigationServiceNavigationMode
    {
        New,
        Back,
        Forward,
        Refresh,
        Reset
    }

    public enum NavigationServiceBackKeyPressedBehavior
    {
        GoBack,
        DoNothing
    }

    public class NavigationServiceNavigationEventArgs : EventArgs
    {
        public NavigationServiceNavigationMode NavigationMode { get; }
        public Type SourcePageType { get; }
        public object Parameter { get; }
        public object Content { get; }

        public NavigationServiceNavigationEventArgs(
            NavigationServiceNavigationMode navigationMode,
            Type sourcePageType,
            object parameter,
            object content)
        {
            NavigationMode = navigationMode;
            SourcePageType = sourcePageType;
            Parameter = parameter;
            Content = content;
        }
    }

    public class NavigationServiceBackKeyPressedEventArgs : EventArgs
    {
        public NavigationServiceBackKeyPressedBehavior Behavior { get; set; } =
            NavigationServiceBackKeyPressedBehavior.GoBack;
    }

    public interface INavigationService
    {
        event EventHandler<NavigationServiceNavigationEventArgs> Navigated;
        event EventHandler<NavigationServiceBackKeyPressedEventArgs> BackKeyPressed;

        Uri CurrentSource { get; }
        IEnumerable<KeyValuePair<string, string>> QueryString { get; }
        object CurrentParameter { get; }
        bool CanGoBack { get; }
        bool CanGoForward { get; }

        bool Navigate(string source);
        bool Navigate(Uri source);
        bool Navigate();
        bool Navigate(object parameter);
        bool Navigate<T>();
        bool Navigate<T>(object parameter);
        bool Navigate(Type type);
        bool Navigate(Type type, object parameter);

        void GoBack();
        void GoForward();
        bool RemoveBackEntry();
        void ClearBackstack();
        void RegisterFrame(object frame);
    }
}
