using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapGen.Render.Skia.WinUI
{
    public partial class MainWindowViewModel : ObservableObject
    {

        [ObservableProperty] private bool _showGridHeightmap;
        [ObservableProperty] private bool _showPackHeightmap = true;
        [ObservableProperty] private bool _showBiomes;
        [ObservableProperty] private bool _showPrecipitation;
        [ObservableProperty] private bool _showTemperature;
        [ObservableProperty] private bool _showShoreline;
        [ObservableProperty] private bool _showRivers;
    }
}
