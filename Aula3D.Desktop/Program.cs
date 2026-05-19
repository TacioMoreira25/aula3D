using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using Aula3D.Desktop.Core.Interfaces;
using Aula3D.Desktop.Core.Services;

namespace Aula3D.Desktop;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);

        // Registro de Serviços
        appBuilder.Services.AddSingleton<IUdpReceiverService, UdpReceiverService>();
        
        appBuilder.Services.AddScoped<ModelViewerService>();
        appBuilder.Services.AddScoped<InteractionProcessor>();

        // Configuração da janela principal
        appBuilder.RootComponents.Add<UI.App>("#app");

        var app = appBuilder.Build();

        app.MainWindow
            .SetTitle("Aula3D Desktop - Vision Control")
            .SetUseOsDefaultSize(false)
            .SetSize(1024, 768)
            .SetLogVerbosity(0)
            .Center();

        AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
        {
            app.MainWindow.ShowMessage("Erro fatal", error.ExceptionObject.ToString());
        };

        app.Run();
    }
}