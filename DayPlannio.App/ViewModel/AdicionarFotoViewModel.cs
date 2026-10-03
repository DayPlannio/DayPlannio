using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace DayPlannio.App.ViewModels;

public partial class AdicionarFotoViewModel : ObservableObject
{
    private readonly INavigation _navigation;
    private readonly string _agendamentoId;
    private readonly string _tipoServico;

    public bool Confirmado { get; private set; }

    public ObservableCollection<FotoPreviewItem> Fotos { get; } = new();

    [ObservableProperty]
    private string fotoStatus = "";

    [ObservableProperty]
    private bool temFoto;

    [ObservableProperty]
    private bool mostrarTodasFotos;

    public IEnumerable<FotoPreviewItem> FotosPreview => Fotos.Take(2);

    public bool TemMaisDeDuasFotos => Fotos.Count > 2;

    public string VerTodasLabel => $"Ver todas ({Fotos.Count})";

    public AdicionarFotoViewModel(
        INavigation navigation,
        string agendamentoId,
        string tipoServico)
    {
        _navigation = navigation;
        _agendamentoId = agendamentoId;
        _tipoServico = tipoServico;

        Fotos.CollectionChanged += (_, _) => AtualizarPropriedadesDerivadas();
    }

    [RelayCommand]
    private async Task AdicionarFoto()
    {
        if (!await PlanoAppService.ExigirPlanoAsync(PlanoAppService.Full))
            return;

        var opcao = await Application.Current.MainPage.DisplayActionSheet(
            $"Adicionar foto - {_tipoServico}",
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

            Fotos.Add(new FotoPreviewItem
            {
                Stream = stream,
                FileName = fileName,
                TempPath = tempPath,
                Preview = ImageSource.FromFile(tempPath)
            });
        }

        AtualizarStatus();
    }

    [RelayCommand]
    private void RemoverFoto(FotoPreviewItem item)
    {
        item.Dispose();
        Fotos.Remove(item);
        AtualizarStatus();

        if (!TemMaisDeDuasFotos)
            MostrarTodasFotos = false;
    }

    [RelayCommand]
    private void ToggleVerTodas()
    {
        MostrarTodasFotos = !MostrarTodasFotos;
    }

    private void AtualizarStatus()
    {
        TemFoto = Fotos.Count > 0;
        FotoStatus = Fotos.Count > 0 ? $"{Fotos.Count} foto(s) selecionada(s)" : "";
    }

    private void AtualizarPropriedadesDerivadas()
    {
        OnPropertyChanged(nameof(FotosPreview));
        OnPropertyChanged(nameof(TemMaisDeDuasFotos));
        OnPropertyChanged(nameof(VerTodasLabel));
    }

    [RelayCommand]
    private async Task Confirmar()
    {
        if (Fotos.Count == 0) return;

        Confirmado = true;

        FotoStatus = "Enviando fotos...";
        var userId = Preferences.Get("userId", string.Empty);
        int enviadas = 0;
        int falhas = 0;

        foreach (var item in Fotos)
        {
            item.Stream.Position = 0;
            var (sucesso, erro, _) = await FotoService.UploadFoto(
                item.Stream, item.FileName, _agendamentoId, userId, null, item.Publica);

            if (sucesso) enviadas++;
            else falhas++;
        }

        foreach (var item in Fotos)
            item.Dispose();
        Fotos.Clear();

        if (falhas > 0)
            await Application.Current.MainPage.DisplayAlertAsync(
                "Aviso", $"{falhas} foto(s) não enviada(s).", "OK");
        else
            await Application.Current.MainPage.DisplayAlertAsync(
                "Foto(s) enviada(s)", $"{enviadas} foto(s) enviada(s) para análise do administrador.", "OK");

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