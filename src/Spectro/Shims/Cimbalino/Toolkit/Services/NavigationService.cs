using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Cimbalino.Toolkit.Services
{
    public class NavigationService : INavigationService
    {
        private Frame _frame;
        private object _currentParameter;

        public event EventHandler<NavigationServiceNavigationEventArgs> Navigated;
        public event EventHandler<NavigationServiceBackKeyPressedEventArgs> BackKeyPressed;

        public Uri CurrentSource => _frame?.CurrentSourcePageType == null ? null : new Uri(_frame.CurrentSourcePageType.FullName, UriKind.Relative);
        public IEnumerable<KeyValuePair<string, string>> QueryString => Array.Empty<KeyValuePair<string, string>>();
        public object CurrentParameter => _currentParameter;
        public bool CanGoBack => _frame?.CanGoBack == true;
        public bool CanGoForward => _frame?.CanGoForward == true;

        public bool Navigate(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            return Navigate(new Uri(source, UriKind.RelativeOrAbsolute));
        }

        public bool Navigate(Uri source)
        {
            if (source == null)
            {
                return false;
            }

            var type = Type.GetType(source.OriginalString);
            return type != null && Navigate(type);
        }
        public bool Navigate() => false;
        public bool Navigate(object parameter) => false;
        public bool Navigate<T>() => Navigate(typeof(T));
        public bool Navigate<T>(object parameter) => Navigate(typeof(T), parameter);
        public bool Navigate(Type type) => Navigate(type, null);
        public bool Navigate(Type type, object parameter)
        {
            _currentParameter = parameter;
            return _frame?.Navigate(type, parameter) == true;
        }

        public void GoBack()
        {
            if (CanGoBack)
            {
                var args = new NavigationServiceBackKeyPressedEventArgs();
                BackKeyPressed?.Invoke(this, args);
                if (args.Behavior == NavigationServiceBackKeyPressedBehavior.GoBack)
                {
                    _frame.GoBack();
                }
            }
        }

        public void GoForward()
        {
            if (CanGoForward)
            {
                _frame.GoForward();
            }
        }

        public bool RemoveBackEntry()
        {
            if (_frame?.BackStackDepth > 0)
            {
                _frame.BackStack.RemoveAt(_frame.BackStackDepth - 1);
                return true;
            }

            return false;
        }

        public void ClearBackstack() => _frame?.BackStack.Clear();

        public void RegisterFrame(object frame)
        {
            if (_frame != null)
            {
                _frame.Navigated -= OnFrameNavigated;
            }

            _frame = frame as Frame;
            if (_frame != null)
            {
                _frame.Navigated += OnFrameNavigated;
            }
        }

        private void OnFrameNavigated(object sender, NavigationEventArgs e)
        {
            _currentParameter = e.Parameter;
            Navigated?.Invoke(this, new NavigationServiceNavigationEventArgs(MapNavigationMode(e.NavigationMode), e.SourcePageType, e.Parameter, null));
        }

        private static NavigationServiceNavigationMode MapNavigationMode(NavigationMode mode)
        {
            return mode switch
            {
                NavigationMode.Back => NavigationServiceNavigationMode.Back,
                NavigationMode.Forward => NavigationServiceNavigationMode.Forward,
                NavigationMode.Refresh => NavigationServiceNavigationMode.Refresh,
                NavigationMode.New => NavigationServiceNavigationMode.New,
                _ => NavigationServiceNavigationMode.Reset
            };
        }
    }
}
