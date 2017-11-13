using System;
using System.Threading.Tasks;
using Spectro.Core.Services;

namespace Spectro.Core.Interfaces
{
    public interface IThemeService
    {
        event EventHandler<ThemeChangedEventArgs> ThemeChanged;
        Task SetTheme(SpectroTheme theme);
        Task Initialize();
        SpectroTheme CurrentTheme { get; }
    }
}