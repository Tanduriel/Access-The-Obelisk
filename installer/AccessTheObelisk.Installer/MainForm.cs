namespace AccessTheObelisk.Installer;

internal sealed class MainForm : Form
{
    private readonly TextBox _gamePathBox = new();
    private readonly Button _browseButton = new();
    private readonly Label _statusLabel = new();
    private readonly Button _installButton = new();
    private readonly Button _updateButton = new();
    private readonly Button _uninstallButton = new();
    private readonly Button _installRussianButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly ModInstaller _installer = new();
    private readonly GitHubReleaseClient _releaseClient = new();

    private string? _gamePath;

    public MainForm()
    {
        Text = "AccessTheObelisk Installer";
        Width = 720;
        Height = 360;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(620, 320);

        Label pathLabel = new()
        {
            Text = "Папка игры Across the Obelisk:",
            AutoSize = true,
            Left = 12,
            Top = 16
        };

        _gamePathBox.Left = 12;
        _gamePathBox.Top = 42;
        _gamePathBox.Width = 540;
        _gamePathBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _gamePathBox.ReadOnly = true;
        _gamePathBox.AccessibleName = "Папка игры";

        _browseButton.Text = "Выбрать папку игры";
        _browseButton.Left = 565;
        _browseButton.Top = 40;
        _browseButton.Width = 130;
        _browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _browseButton.Click += BrowseButton_Click;

        _statusLabel.Left = 12;
        _statusLabel.Top = 82;
        _statusLabel.Width = 680;
        _statusLabel.Height = 90;
        _statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _statusLabel.Text = "Поиск папки игры...";

        _installButton.Text = "Установить";
        _installButton.Left = 12;
        _installButton.Top = 185;
        _installButton.Width = 120;
        _installButton.Click += InstallButton_Click;

        _updateButton.Text = "Обновить";
        _updateButton.Left = 144;
        _updateButton.Top = 185;
        _updateButton.Width = 120;
        _updateButton.Click += UpdateButton_Click;

        _uninstallButton.Text = "Удалить";
        _uninstallButton.Left = 276;
        _uninstallButton.Top = 185;
        _uninstallButton.Width = 120;
        _uninstallButton.Click += UninstallButton_Click;

        _installRussianButton.Text = "Установить русскую локализацию игры";
        _installRussianButton.Left = 408;
        _installRussianButton.Top = 185;
        _installRussianButton.Width = 285;
        _installRussianButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _installRussianButton.Click += InstallRussianButton_Click;

        _progressBar.Left = 12;
        _progressBar.Top = 238;
        _progressBar.Width = 680;
        _progressBar.Height = 24;
        _progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        Controls.Add(pathLabel);
        Controls.Add(_gamePathBox);
        Controls.Add(_browseButton);
        Controls.Add(_statusLabel);
        Controls.Add(_installButton);
        Controls.Add(_updateButton);
        Controls.Add(_uninstallButton);
        Controls.Add(_installRussianButton);
        Controls.Add(_progressBar);

        Load += MainForm_Load;
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        string? detectedPath = GameLocator.FindGameDirectory();
        if (!string.IsNullOrWhiteSpace(detectedPath))
        {
            SetGamePath(detectedPath);
        }
        else
        {
            RefreshButtons();
            _statusLabel.Text = "Папка игры не найдена автоматически. Выберите её вручную.";
        }
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "Выберите папку Across the Obelisk",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (!string.IsNullOrWhiteSpace(_gamePath) && Directory.Exists(_gamePath))
        {
            dialog.SelectedPath = _gamePath;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetGamePath(dialog.SelectedPath);
        }
    }

    private async void InstallButton_Click(object? sender, EventArgs e)
    {
        await InstallOrUpdateModAsync();
    }

    private async void UpdateButton_Click(object? sender, EventArgs e)
    {
        await InstallOrUpdateModAsync();
    }

    private void UninstallButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_gamePath))
        {
            return;
        }

        DialogResult result = MessageBox.Show(this, "Удалить AccessTheObelisk из выбранной папки игры?", "Удаление мода", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            _installer.UninstallPackage(_gamePath, InstallerConstants.ModName);
            MessageBox.Show(this, "Мод удалён.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetGamePath(_gamePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка удаления", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void InstallRussianButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_gamePath))
        {
            return;
        }

        try
        {
            SetBusy(true, "Подготовка русской локализации...");
            string zipPath = await GetRussianLocalizationZipAsync();
            _installer.InstallRussianLocalization(_gamePath, zipPath);
            MessageBox.Show(this, "Русская локализация игры установлена.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetGamePath(_gamePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка установки русской локализации", MessageBoxButtons.OK, MessageBoxIcon.Error);
            RefreshButtons();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task InstallOrUpdateModAsync()
    {
        if (string.IsNullOrWhiteSpace(_gamePath))
        {
            return;
        }

        try
        {
            SetBusy(true, "Получение списка версий с GitHub...");
            IReadOnlyList<ReleaseInfo> releases = await _releaseClient.GetReleasesAsync(CancellationToken.None);
            releases = releases.Where(HasModAsset).ToList();

            if (releases.Count == 0)
            {
                MessageBox.Show(this, "В GitHub Releases пока нет архивов AccessTheObelisk.", "Версии не найдены", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetBusy(false);
            using VersionSelectionForm versionForm = new(releases);
            if (versionForm.ShowDialog(this) != DialogResult.OK || versionForm.SelectedRelease == null)
            {
                return;
            }

            ReleaseInfo selectedRelease = versionForm.SelectedRelease;
            using ChangelogForm changelogForm = new(selectedRelease);
            if (changelogForm.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            ReleaseAssetInfo asset = selectedRelease.Assets.First(IsModAsset);
            SetBusy(true, "Скачивание выбранной версии...");
            string tempFile = Path.Combine(Path.GetTempPath(), asset.Name);
            await _releaseClient.DownloadFileAsync(asset.DownloadUrl, tempFile, new Progress<int>(value => _progressBar.Value = value), CancellationToken.None);

            SetBusy(true, "Установка мода...");
            _installer.InstallPackage(_gamePath, tempFile, InstallerConstants.ModName, selectedRelease.TagName.TrimStart('v', 'V'));
            MessageBox.Show(this, "AccessTheObelisk установлен.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetGamePath(_gamePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка установки", MessageBoxButtons.OK, MessageBoxIcon.Error);
            RefreshButtons();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<string> GetRussianLocalizationZipAsync()
    {
        if (File.Exists(InstallerConstants.LocalRussianLocalizationPath))
        {
            return InstallerConstants.LocalRussianLocalizationPath;
        }

        IReadOnlyList<ReleaseInfo> releases = await _releaseClient.GetReleasesAsync(CancellationToken.None);
        ReleaseAssetInfo? asset = releases
            .SelectMany(release => release.Assets)
            .FirstOrDefault(asset => string.Equals(asset.Name, InstallerConstants.RussianLocalizationAssetName, StringComparison.OrdinalIgnoreCase));

        if (asset == null)
        {
            throw new InvalidOperationException($"Не найден архив {InstallerConstants.RussianLocalizationAssetName}. Добавьте его в GitHub Release или положите файл по пути {InstallerConstants.LocalRussianLocalizationPath}.");
        }

        string tempFile = Path.Combine(Path.GetTempPath(), asset.Name);
        await _releaseClient.DownloadFileAsync(asset.DownloadUrl, tempFile, new Progress<int>(value => _progressBar.Value = value), CancellationToken.None);
        return tempFile;
    }

    private void SetGamePath(string path)
    {
        _gamePath = path;
        _gamePathBox.Text = path;

        GameInstallState state = _installer.GetState(path);
        string modState = state.IsModInstalled
            ? $"Мод установлен. Версия: {FormatVersion(state.InstalledVersion)}."
            : "Мод не установлен.";
        string bepinState = state.HasBepInEx ? "BepInEx найден." : "BepInEx не найден.";
        string ruState = state.IsRussianLocalizationInstalled ? "Русская локализация игры установлена." : "Русская локализация игры не установлена.";

        _statusLabel.Text = state.IsValidGame
            ? $"Папка игры выбрана. {bepinState} {modState} {ruState}"
            : "Выбранная папка не похожа на папку Across the Obelisk.";

        RefreshButtons();
    }

    private void RefreshButtons()
    {
        bool hasValidPath = !string.IsNullOrWhiteSpace(_gamePath) && GameLocator.IsGameDirectory(_gamePath);
        bool modInstalled = hasValidPath && _installer.GetState(_gamePath!).IsModInstalled;

        _installButton.Enabled = hasValidPath && !modInstalled;
        _updateButton.Enabled = hasValidPath && modInstalled;
        _uninstallButton.Enabled = hasValidPath && modInstalled;
        _installRussianButton.Enabled = hasValidPath;
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _browseButton.Enabled = !busy;
        _installButton.Enabled = !busy && _installButton.Enabled;
        _updateButton.Enabled = !busy && _updateButton.Enabled;
        _uninstallButton.Enabled = !busy && _uninstallButton.Enabled;
        _installRussianButton.Enabled = !busy && _installRussianButton.Enabled;

        if (!busy)
        {
            _progressBar.Value = 0;
            RefreshButtons();
        }
        else if (!string.IsNullOrWhiteSpace(status))
        {
            _statusLabel.Text = status;
        }
    }

    private static bool HasModAsset(ReleaseInfo release)
    {
        return release.Assets.Any(IsModAsset);
    }

    private static bool IsModAsset(ReleaseAssetInfo asset)
    {
        return asset.Name.StartsWith("AccessTheObelisk-v", StringComparison.OrdinalIgnoreCase)
            && asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatVersion(string version)
    {
        return string.IsNullOrWhiteSpace(version) ? "неизвестна" : version;
    }
}
