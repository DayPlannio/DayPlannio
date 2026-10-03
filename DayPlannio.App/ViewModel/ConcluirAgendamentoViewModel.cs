using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;
using System.Collections.ObjectModel;

namespace DayPlannio.App.ViewModels;

public partial class FotoPreviewItem : ObservableObject, IDisposable
{
    public Stream Stream { get; set; } = null!;
    public string FileName { get; set; } = "";
    public string TempPath { get; set; } = "";

    [ObservableProperty]
    private ImageSource? preview;

    [ObservableProperty]
    private bool publica;

    public void Dispose()
    {
        Stream?.Dispose();
        if (!string.IsNullOrEmpty(TempPath) && File.Exists(TempPath))
            try { File.Delete(TempPath); } catch { }
    }
}

public partial class ConcluirAgendamentoViewModel : ObservableObject
{
    private readonly INavigation _navigation;
    private readonly string _agendamentoId;
    private readonly string _prestadorId;

    public bool Confirmado { get; private set; }

    public ObservableCollection<FotoPreviewItem> Fotos { get; } = new();

    [ObservableProperty]
    private FormattedString mensagem;

    [ObservableProperty]
    private string fotoStatus = "";

    [ObservableProperty]
    private bool temFoto;

    [ObservableProperty]
    private string contadorFotos = "";

    public ConcluirAgendamentoViewModel(
        INavigation navigation,
        string agendamentoId,
        string prestadorId,
        string nomeCliente,
        string servico)
    {
        _navigation = navigation;
        _agendamentoId = agendamentoId;
        _prestadorId = prestadorId;

        Mensagem = new FormattedString
        {
            Spans =
            {
                new Span { Text = "Tem certeza que deseja concluir o agendamento de " },
                new Span { Text = nomeCliente, FontAttributes = FontAttributes.Bold },
                new Span { Text = " - " },
                new Span { Text = servico, FontAttributes = FontAttributes.Bold },
                new Span { Text = "?" }
            }
        };
    }

    [RelayCommand]
    private async Task AdicionarFoto()
    {
        if (!await PlanoAppService.ExigirPlanoAsync(PlanoAppService.Full))
            return;

        var opcao = await Application.Current.MainPage.DisplayActionSheet(
            "Adicionar foto do serviço",
            "Cancelar",
            null,
            "Tirar foto",
            "Escolher da galeria");

        List<(Stream stream, string fileName)> novas = new();

        if (opcao == "Tirar foto")
        {
            if (MediaPicker.Default.IsCaptureSupported)
            {
                var foto = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
                {
                    Title = "Foto do serviço"
                });
                if (foto != null)
                {
                    novas.Add((await foto.OpenReadAsync(), foto.FileName));
                }
            }
            else
            {
                await Application.Current.MainPage.DisplayAlertAsync(
                    "Aviso", "Captura de foto não suportada neste dispositivo.", "OK");
                return;
            }
        }
        else if (opcao == "Escolher da galeria")
        {
            var fotos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                Title = "Fotos do serviço"
            });

            if (fotos != null)
            {
                foreach (var foto in fotos)
                {
                    novas.Add((await foto.OpenReadAsync(), foto.FileName));
                }
            }
        }
        else
        {
            return;
        }

        if (novas.Count == 0) return;

        var tempDir = Path.Combine(FileSystem.CacheDirectory, "fotos_preview");
        Directory.CreateDirectory(tempDir);

        foreach (var (stream, fileName) in novas)
        {
            var tempPath = Path.Combine(tempDir, $"{Guid.NewGuid()}_{fileName}");

            using (var fs = File.Create(tempPath))
            {
                stream.Position = 0;
                await stream.CopyToAsync(fs);
            }

            stream.Position = 0;

            var item = new FotoPreviewItem
            {
                Stream = stream,
                FileName = fileName,
                TempPath = tempPath,
                Preview = ImageSource.FromFile(tempPath)
            };

            Fotos.Add(item);
        }

        AtualizarStatus();
    }

    [RelayCommand]
    private void RemoverFoto(FotoPreviewItem item)
    {
        item.Dispose();
        Fotos.Remove(item);
        AtualizarStatus();
    }

    private void AtualizarStatus()
    {
        TemFoto = Fotos.Count > 0;
        FotoStatus = Fotos.Count > 0 ? $"{Fotos.Count} foto(s) selecionada(s)" : "";
        ContadorFotos = $"{Fotos.Count} foto(s)";
    }

    [RelayCommand]
    private async Task Concluir()
    {
        Confirmado = true;

        if (Fotos.Count > 0)
        {
            FotoStatus = "Enviando fotos...";
            int enviadas = 0;
            int falhas = 0;

            foreach (var item in Fotos)
            {
                item.Stream.Position = 0;
                var (sucesso, erro, _) = await FotoService.UploadFoto(
                    item.Stream, item.FileName, _agendamentoId, _prestadorId, null, item.Publica);

                if (sucesso) enviadas++;
                else falhas++;
            }

            if (falhas > 0)
            {
                await Application.Current.MainPage.DisplayAlertAsync(
                    "Aviso", $"{falhas} foto(s) não enviada(s).", "OK");
            }
            else if (enviadas > 0)
            {
                await Application.Current.MainPage.DisplayAlertAsync(
                    "Foto(s) enviada(s)", $"{enviadas} foto(s) enviada(s) para análise do administrador.", "OK");
            }
        }

        foreach (var item in Fotos)
            item.Dispose();
        Fotos.Clear();

        await _navigation.PopModalAsync();
    }

    [RelayCommand]
    private async Task Cancelar()
    {
        Confirmado = false;
        foreach (var item in Fotos)
            item.Dispose();
        Fotos.Clear();
        await _navigation.PopModalAsync();
    }
}
