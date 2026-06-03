namespace AccessTheObelisk.Installer;

internal sealed class ChangelogForm : Form
{
    public ChangelogForm(ReleaseInfo release)
    {
        Text = $"Изменения {release.TagName}";
        StartPosition = FormStartPosition.CenterParent;
        Width = 700;
        Height = 520;
        MinimizeBox = false;
        MaximizeBox = false;

        TextBox changelogBox = new()
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Left = 12,
            Top = 12,
            Width = 660,
            Height = 400,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Text = string.IsNullOrWhiteSpace(release.Changelog) ? "Для этой версии чейнджлог пока не заполнен." : release.Changelog,
            AccessibleName = "Чейнджлог выбранной версии"
        };

        Button continueButton = new()
        {
            Text = "Продолжить",
            Left = 440,
            Top = 426,
            Width = 110,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.OK
        };

        Button cancelButton = new()
        {
            Text = "Отмена",
            Left = 562,
            Top = 426,
            Width = 110,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.Cancel
        };

        Controls.Add(changelogBox);
        Controls.Add(continueButton);
        Controls.Add(cancelButton);
        AcceptButton = continueButton;
        CancelButton = cancelButton;
    }
}
