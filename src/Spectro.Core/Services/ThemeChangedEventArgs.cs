using System;

namespace Spectro.Core.Services
{
    public class ThemeChangedEventArgs : EventArgs
    {
        public SpectroTheme Theme { get; }

        public ThemeChangedEventArgs(SpectroTheme theme)
        {
            Theme = theme;
        }
    }
}