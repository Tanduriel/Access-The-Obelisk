namespace AccessTheObelisk.Installer;

internal sealed class VersionSelectionForm : Form
{
    private readonly ListBox _versionsList = new();
    private readonly Button _okButton = new();
    private readonly Button _cancelButton = new();

    public VersionSelectionForm(IReadOnlyList<ReleaseInfo> releases)
    {
        Text = "Выбор версии AccessTheObelisk";
        StartPosition = FormStartPosition.CenterParent;
        Width = 560;
        Height = 380;
        MinimizeBox = false;
        MaximizeBox = false;

        Label label = new()
        {
            Text = "Выберите версию мода:",
            AutoSize = true,
            Left = 12,
            Top = 12
        };

        _versionsList.Left = 12;
        _versionsList.Top = 40;
        _versionsList.Width = 520;
        _versionsList.Height = 240;
        _versionsList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _versionsList.AccessibleName = "Список версий мода";
        foreach (ReleaseInfo release in releases)
        {
            _versionsList.Items.Add(release);
        }

        if (_versionsList.Items.Count > 0)
        {
            _versionsList.SelectedIndex = 0;
        }

        _okButton.Text = "Продолжить";
        _okButton.Left = 300;
        _okButton.Top = 295;
        _okButton.Width = 110;
        _okButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _okButton.DialogResult = DialogResult.OK;

        _cancelButton.Text = "Отмена";
        _cancelButton.Left = 422;
        _cancelButton.Top = 295;
        _cancelButton.Width = 110;
        _cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _cancelButton.DialogResult = DialogResult.Cancel;

        Controls.Add(label);
        Controls.Add(_versionsList);
        Controls.Add(_okButton);
        Controls.Add(_cancelButton);
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    public ReleaseInfo? SelectedRelease => _versionsList.SelectedItem as ReleaseInfo;
}
