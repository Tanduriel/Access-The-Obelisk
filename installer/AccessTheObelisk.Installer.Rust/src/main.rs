#![windows_subsystem = "windows"]

use anyhow::{anyhow, bail, Context, Result};
use serde::{Deserialize, Serialize};
use std::cell::Cell;
use std::ffi::c_void;
use std::fs::{self, File};
use std::io::{self, Read, Write};
use std::path::{Path, PathBuf};
use std::ptr;
use std::sync::Arc;
use std::sync::mpsc::{self, Receiver, Sender};
use std::thread;
use windows_sys::Win32::Foundation::{BOOL, HWND, LPARAM, LRESULT, MAX_PATH, TRUE, WPARAM};
use windows_sys::Win32::Globalization::GetUserDefaultUILanguage;
use windows_sys::Win32::Graphics::Gdi::DEFAULT_GUI_FONT;
use windows_sys::Win32::System::Com::{CoInitializeEx, CoTaskMemFree, COINIT_APARTMENTTHREADED};
use windows_sys::Win32::System::LibraryLoader::GetModuleHandleW;
use windows_sys::Win32::UI::Controls::{InitCommonControls, PBM_SETPOS, PBM_SETRANGE32};
use windows_sys::Win32::UI::Shell::{
    SHBrowseForFolderW, SHGetPathFromIDListW, BIF_NEWDIALOGSTYLE, BIF_RETURNONLYFSDIRS,
    BROWSEINFOW,
};
use windows_sys::Win32::UI::Input::KeyboardAndMouse::{EnableWindow, GetKeyState, SetFocus};
use windows_sys::Win32::UI::WindowsAndMessaging::{
    CreateWindowExW, DefWindowProcW, DestroyWindow, DispatchMessageW,
    GetAncestor, GetMessageW, GetWindowLongPtrW,
    IsDialogMessageW, LoadCursorW, MessageBoxW, PostMessageW, PostQuitMessage, RegisterClassW,
    SendMessageW, SetWindowLongPtrW, SetWindowTextW, ShowWindow, TranslateMessage,
    CREATESTRUCTW, CW_USEDEFAULT, GA_ROOT, GWLP_USERDATA, HMENU, IDC_ARROW, LB_ADDSTRING,
    LB_GETCURSEL, LB_SETCURSEL, LBS_NOTIFY, MB_ICONERROR, MB_ICONINFORMATION, MB_ICONQUESTION,
    MB_OK, MB_YESNO, MSG, SW_SHOW, WINDOW_EX_STYLE, WM_APP, WM_CLOSE, WM_COMMAND, WM_CREATE,
    WM_DESTROY, WM_NCCREATE, WNDCLASSW, WS_BORDER, WS_CAPTION, WS_CHILD, WS_EX_CLIENTEDGE,
    WS_OVERLAPPED, WS_SYSMENU, WS_TABSTOP, WS_THICKFRAME, WS_VISIBLE, WS_VSCROLL,
};
use winreg::enums::HKEY_CURRENT_USER;
use winreg::RegKey;
use zip::ZipArchive;

const MOD_NAME: &str = "AccessTheObelisk";
const GITHUB_OWNER: &str = "tanduriel";
const GITHUB_REPOSITORY: &str = "Access-The-Obelisk";
const INSTALLER_VERSION: &str = "0.4.0";
const BEPINEX_PACK_VERSION: &str = "5.4.23";
const BEPINEX_PACK_PREFIX: &str = "BepInExPack_AcrossTheObelisk/";
const RUSSIAN_LOCALIZATION_ASSET: &str = "AcrossTheObelisk_Russian_v1.2.1.zip";
const LOCAL_RUSSIAN_LOCALIZATION_PATH: &str =
    r"C:\Users\Incognitus\Downloads\AcrossTheObelisk_Russian_v1.2.1.zip";
const WM_WORKER: u32 = WM_APP + 1;

const ID_BROWSE: i32 = 1001;
const ID_INSTALL: i32 = 1002;
const ID_UPDATE: i32 = 1003;
const ID_REMOVE: i32 = 1004;
const ID_INSTALL_RU: i32 = 1005;
const ID_VERSION_LIST: i32 = 2001;
const ID_OK: i32 = 2002;
const ID_CANCEL: i32 = 2003;
const ES_MULTILINE: u32 = 0x0004;
const ES_AUTOVSCROLL: u32 = 0x0040;
const ES_READONLY: u32 = 0x0800;
const EM_SETSEL: u32 = 0x00B1;
const EM_SCROLLCARET: u32 = 0x00B7;
const VK_TAB_KEY: usize = 0x09;
const VK_SHIFT_KEY: i32 = 0x10;
const WM_KEYDOWN_MSG: u32 = 0x0100;

#[derive(Clone, Copy)]
enum Lang {
    En,
    Ru,
}

#[derive(Clone, Copy)]
enum Text {
    WindowTitle,
    GamePathLabel,
    Browse,
    Searching,
    NotFound,
    Install,
    Update,
    Remove,
    InstallRussian,
    SelectFolderTitle,
    RemoveQuestionTitle,
    RemoveQuestion,
    Done,
    Removed,
    RemoveError,
    PreparingRussian,
    RussianInstalled,
    FetchingVersions,
    NoVersionsTitle,
    NoVersions,
    DownloadingVersion,
    InstallingMod,
    InstallingBepInEx,
    ModInstalled,
    InstallError,
    VersionWindowTitle,
    VersionLabel,
    Continue,
    Cancel,
    NoChangelog,
    ChangelogTitle,
    GameFolderSelected,
    InvalidGameFolder,
    ModInstalledState,
    ModNotInstalledState,
    BepInExFound,
    BepInExMissing,
    RussianFound,
    RussianMissing,
    UnknownVersion,
}

fn tr(lang: Lang, text: Text) -> &'static str {
    match (lang, text) {
        (Lang::En, Text::WindowTitle) => "AccessTheObelisk Installer",
        (Lang::En, Text::GamePathLabel) => "Across the Obelisk game folder:",
        (Lang::En, Text::Browse) => "Choose game folder",
        (Lang::En, Text::Searching) => "Searching for the game folder...",
        (Lang::En, Text::NotFound) => "The game folder was not found automatically. Choose it manually.",
        (Lang::En, Text::Install) => "Install",
        (Lang::En, Text::Update) => "Update",
        (Lang::En, Text::Remove) => "Remove",
        (Lang::En, Text::InstallRussian) => "Install Russian game localization",
        (Lang::En, Text::SelectFolderTitle) => "Choose the Across the Obelisk folder",
        (Lang::En, Text::RemoveQuestionTitle) => "Remove mod",
        (Lang::En, Text::RemoveQuestion) => "Remove AccessTheObelisk from the selected game folder?",
        (Lang::En, Text::Done) => "Done",
        (Lang::En, Text::Removed) => "The mod was removed.",
        (Lang::En, Text::RemoveError) => "Remove error",
        (Lang::En, Text::PreparingRussian) => "Preparing Russian localization...",
        (Lang::En, Text::RussianInstalled) => "Russian game localization was installed.",
        (Lang::En, Text::FetchingVersions) => "Fetching versions from GitHub...",
        (Lang::En, Text::NoVersionsTitle) => "No versions found",
        (Lang::En, Text::NoVersions) => "GitHub Releases do not contain AccessTheObelisk archives yet.",
        (Lang::En, Text::DownloadingVersion) => "Downloading the selected version...",
        (Lang::En, Text::InstallingMod) => "Installing mod...",
        (Lang::En, Text::InstallingBepInEx) => "Installing BepInEx runtime...",
        (Lang::En, Text::ModInstalled) => "AccessTheObelisk was installed.",
        (Lang::En, Text::InstallError) => "Install error",
        (Lang::En, Text::VersionWindowTitle) => "AccessTheObelisk version selection",
        (Lang::En, Text::VersionLabel) => "Choose mod version:",
        (Lang::En, Text::Continue) => "Continue",
        (Lang::En, Text::Cancel) => "Cancel",
        (Lang::En, Text::NoChangelog) => "This version does not have a changelog yet.",
        (Lang::En, Text::ChangelogTitle) => "Changes",
        (Lang::En, Text::GameFolderSelected) => "Game folder selected.",
        (Lang::En, Text::InvalidGameFolder) => "The selected folder does not look like an Across the Obelisk folder.",
        (Lang::En, Text::ModInstalledState) => "Mod installed. Version:",
        (Lang::En, Text::ModNotInstalledState) => "Mod is not installed.",
        (Lang::En, Text::BepInExFound) => "BepInEx found.",
        (Lang::En, Text::BepInExMissing) => "BepInEx not found.",
        (Lang::En, Text::RussianFound) => "Russian game localization installed.",
        (Lang::En, Text::RussianMissing) => "Russian game localization not installed.",
        (Lang::En, Text::UnknownVersion) => "unknown",
        (Lang::Ru, Text::WindowTitle) => "Установщик AccessTheObelisk",
        (Lang::Ru, Text::GamePathLabel) => "Папка игры Across the Obelisk:",
        (Lang::Ru, Text::Browse) => "Выбрать папку игры",
        (Lang::Ru, Text::Searching) => "Поиск папки игры...",
        (Lang::Ru, Text::NotFound) => "Папка игры не найдена автоматически. Выберите её вручную.",
        (Lang::Ru, Text::Install) => "Установить",
        (Lang::Ru, Text::Update) => "Обновить",
        (Lang::Ru, Text::Remove) => "Удалить",
        (Lang::Ru, Text::InstallRussian) => "Установить русскую локализацию игры",
        (Lang::Ru, Text::SelectFolderTitle) => "Выберите папку Across the Obelisk",
        (Lang::Ru, Text::RemoveQuestionTitle) => "Удаление мода",
        (Lang::Ru, Text::RemoveQuestion) => "Удалить AccessTheObelisk из выбранной папки игры?",
        (Lang::Ru, Text::Done) => "Готово",
        (Lang::Ru, Text::Removed) => "Мод удалён.",
        (Lang::Ru, Text::RemoveError) => "Ошибка удаления",
        (Lang::Ru, Text::PreparingRussian) => "Подготовка русской локализации...",
        (Lang::Ru, Text::RussianInstalled) => "Русская локализация игры установлена.",
        (Lang::Ru, Text::FetchingVersions) => "Получение списка версий с GitHub...",
        (Lang::Ru, Text::NoVersionsTitle) => "Версии не найдены",
        (Lang::Ru, Text::NoVersions) => "В GitHub Releases пока нет архивов AccessTheObelisk.",
        (Lang::Ru, Text::DownloadingVersion) => "Скачивание выбранной версии...",
        (Lang::Ru, Text::InstallingMod) => "Установка мода...",
        (Lang::Ru, Text::InstallingBepInEx) => "Установка среды BepInEx...",
        (Lang::Ru, Text::ModInstalled) => "AccessTheObelisk установлен.",
        (Lang::Ru, Text::InstallError) => "Ошибка установки",
        (Lang::Ru, Text::VersionWindowTitle) => "Выбор версии AccessTheObelisk",
        (Lang::Ru, Text::VersionLabel) => "Выберите версию мода:",
        (Lang::Ru, Text::Continue) => "Продолжить",
        (Lang::Ru, Text::Cancel) => "Отмена",
        (Lang::Ru, Text::NoChangelog) => "Для этой версии чейнджлог пока не заполнен.",
        (Lang::Ru, Text::ChangelogTitle) => "Изменения",
        (Lang::Ru, Text::GameFolderSelected) => "Папка игры выбрана.",
        (Lang::Ru, Text::InvalidGameFolder) => "Выбранная папка не похожа на папку Across the Obelisk.",
        (Lang::Ru, Text::ModInstalledState) => "Мод установлен. Версия:",
        (Lang::Ru, Text::ModNotInstalledState) => "Мод не установлен.",
        (Lang::Ru, Text::BepInExFound) => "BepInEx найден.",
        (Lang::Ru, Text::BepInExMissing) => "BepInEx не найден.",
        (Lang::Ru, Text::RussianFound) => "Русская локализация игры установлена.",
        (Lang::Ru, Text::RussianMissing) => "Русская локализация игры не установлена.",
        (Lang::Ru, Text::UnknownVersion) => "неизвестна",
    }
}

#[derive(Debug, Clone, Deserialize)]
struct GitHubRelease {
    tag_name: String,
    name: Option<String>,
    body: Option<String>,
    assets: Vec<GitHubAsset>,
}

#[derive(Debug, Clone, Deserialize)]
struct GitHubAsset {
    name: String,
    browser_download_url: String,
}

#[derive(Debug, Clone)]
struct ReleaseInfo {
    tag_name: String,
    name: String,
    changelog: String,
    assets: Vec<ReleaseAssetInfo>,
}

#[derive(Debug, Clone)]
struct ReleaseAssetInfo {
    name: String,
    download_url: String,
}

impl ReleaseInfo {
    fn display_name(&self) -> String {
        if self.name.trim().is_empty() {
            self.tag_name.clone()
        } else {
            format!("{} ({})", self.name, self.tag_name)
        }
    }
}

#[derive(Debug, Deserialize, Serialize)]
struct PackageManifest {
    #[serde(rename = "packageId")]
    package_id: String,
    name: String,
    version: String,
    files: Vec<PackageFile>,
}

#[derive(Debug, Deserialize, Serialize)]
struct PackageFile {
    path: String,
    sha256: String,
}

#[derive(Debug, Deserialize, Serialize)]
struct InstalledPackage {
    #[serde(rename = "PackageId")]
    package_id: String,
    #[serde(rename = "Version")]
    version: String,
    #[serde(rename = "Files")]
    files: Vec<String>,
}

#[derive(Debug, Clone)]
struct GameInstallState {
    is_valid_game: bool,
    has_bepinex: bool,
    is_mod_installed: bool,
    installed_version: String,
    is_russian_localization_installed: bool,
}

enum WorkerMessage {
    Status(String),
    Progress(u32),
    Finished(Result<WorkerResult, String>),
}

enum WorkerResult {
    Installed,
    RussianInstalled,
}

enum Job {
    Install {
        game_path: PathBuf,
        release: ReleaseInfo,
        asset: ReleaseAssetInfo,
    },
    InstallRussian {
        game_path: PathBuf,
    },
}

struct App {
    lang: Lang,
    hwnd: HWND,
    path_box: HWND,
    status_label: HWND,
    install_button: HWND,
    update_button: HWND,
    remove_button: HWND,
    install_ru_button: HWND,
    browse_button: HWND,
    progress: HWND,
    game_path: Option<PathBuf>,
    busy: bool,
    tx: Sender<WorkerMessage>,
    rx: Receiver<WorkerMessage>,
}

fn main() {
    unsafe {
        CoInitializeEx(ptr::null_mut(), COINIT_APARTMENTTHREADED as u32);
        InitCommonControls();
    }

    let lang = system_lang();
    if let Err(err) = run_app(lang) {
        message_box(ptr::null_mut(), lang, Text::WindowTitle, &err.to_string(), MB_OK | MB_ICONERROR);
    }
}

fn run_app(lang: Lang) -> Result<()> {
    let (tx, rx) = mpsc::channel();
    let mut app = Box::new(App {
        lang,
        hwnd: ptr::null_mut(),
        path_box: ptr::null_mut(),
        status_label: ptr::null_mut(),
        install_button: ptr::null_mut(),
        update_button: ptr::null_mut(),
        remove_button: ptr::null_mut(),
        install_ru_button: ptr::null_mut(),
        browse_button: ptr::null_mut(),
        progress: ptr::null_mut(),
        game_path: None,
        busy: false,
        tx,
        rx,
    });

    let hinstance = unsafe { GetModuleHandleW(ptr::null()) };
    let class_name = wide("AccessTheObeliskInstallerWindow");
    let wc = WNDCLASSW {
        lpfnWndProc: Some(main_wnd_proc),
        hInstance: hinstance,
        lpszClassName: class_name.as_ptr(),
        hCursor: unsafe { LoadCursorW(ptr::null_mut(), IDC_ARROW) },
        ..unsafe { std::mem::zeroed() }
    };
    unsafe {
        RegisterClassW(&wc);
    }

    let app_ptr = app.as_mut() as *mut App;
    let hwnd = unsafe {
        CreateWindowExW(
            0,
            class_name.as_ptr(),
            wide(tr(lang, Text::WindowTitle)).as_ptr(),
            WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            720,
            360,
            ptr::null_mut(),
            ptr::null_mut(),
            hinstance,
            app_ptr.cast(),
        )
    };
    if hwnd.is_null() {
        bail!("Failed to create installer window");
    }

    app.hwnd = hwnd;
    let _keep_alive = app;

    unsafe {
        ShowWindow(hwnd, SW_SHOW);
    }

    let mut msg: MSG = unsafe { std::mem::zeroed() };
    while unsafe { GetMessageW(&mut msg, ptr::null_mut(), 0, 0) } > 0 {
        unsafe {
            if IsDialogMessageW(GetAncestor(msg.hwnd, GA_ROOT), &mut msg) == 0 {
                TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
        }
    }

    Ok(())
}

extern "system" fn main_wnd_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe {
        if msg == WM_NCCREATE {
            let create = lparam as *const CREATESTRUCTW;
            let app = (*create).lpCreateParams as *mut App;
            SetWindowLongPtrW(hwnd, GWLP_USERDATA, app as isize);
            (*app).hwnd = hwnd;
            return DefWindowProcW(hwnd, msg, wparam, lparam);
        }

        let app_ptr = GetWindowLongPtrW(hwnd, GWLP_USERDATA) as *mut App;
        if app_ptr.is_null() {
            return DefWindowProcW(hwnd, msg, wparam, lparam);
        }

        let app = &mut *app_ptr;
        match msg {
            WM_CREATE => {
                app.create_controls();
                if let Some(path) = find_game_directory() {
                    app.set_game_path(path);
                } else {
                    set_text(app.status_label, tr(app.lang, Text::NotFound));
                    app.refresh_buttons();
                }
                0
            }
            WM_COMMAND => {
                let id = (wparam & 0xffff) as i32;
                match id {
                    ID_BROWSE => app.browse_for_game_path(),
                    ID_INSTALL | ID_UPDATE => app.install_or_update_mod(),
                    ID_REMOVE => app.uninstall_mod(),
                    ID_INSTALL_RU => app.install_russian_localization(),
                    _ => {}
                }
                0
            }
            WM_WORKER => {
                app.process_worker_messages();
                0
            }
            WM_CLOSE => {
                DestroyWindow(hwnd);
                0
            }
            WM_DESTROY => {
                PostQuitMessage(0);
                0
            }
            _ => DefWindowProcW(hwnd, msg, wparam, lparam),
        }
    }
}

impl App {
    unsafe fn create_controls(&mut self) {
        let font = SendMessageW(
            CreateWindowExW(
                0,
                wide("STATIC").as_ptr(),
                ptr::null(),
                WS_CHILD,
                0,
                0,
                0,
                0,
                self.hwnd,
                ptr::null_mut(),
                ptr::null_mut(),
                ptr::null_mut(),
            ),
            0x0031,
            DEFAULT_GUI_FONT as WPARAM,
            0,
        );

        create_label(self.hwnd, tr(self.lang, Text::GamePathLabel), 12, 16, 500, 24);
        self.path_box = create_control(
            "EDIT",
            "",
            WS_CHILD | WS_VISIBLE | WS_BORDER | WS_TABSTOP,
            WS_EX_CLIENTEDGE,
            12,
            42,
            540,
            26,
            self.hwnd,
            0,
        );
        SendMessageW(self.path_box, 0x00CF, 1, 0);

        self.browse_button = create_button(self.hwnd, tr(self.lang, Text::Browse), 565, 40, 130, 30, ID_BROWSE);
        self.status_label = create_label(self.hwnd, tr(self.lang, Text::Searching), 12, 82, 680, 90);
        self.install_button = create_button(self.hwnd, tr(self.lang, Text::Install), 12, 185, 120, 32, ID_INSTALL);
        self.update_button = create_button(self.hwnd, tr(self.lang, Text::Update), 144, 185, 120, 32, ID_UPDATE);
        self.remove_button = create_button(self.hwnd, tr(self.lang, Text::Remove), 276, 185, 120, 32, ID_REMOVE);
        self.install_ru_button =
            create_button(self.hwnd, tr(self.lang, Text::InstallRussian), 408, 185, 285, 32, ID_INSTALL_RU);
        self.progress = create_control(
            "msctls_progress32",
            "",
            WS_CHILD | WS_VISIBLE,
            0,
            12,
            238,
            680,
            24,
            self.hwnd,
            0,
        );
        SendMessageW(self.progress, PBM_SETRANGE32, 0, 100);

        for handle in [
            self.path_box,
            self.browse_button,
            self.status_label,
            self.install_button,
            self.update_button,
            self.remove_button,
            self.install_ru_button,
            self.progress,
        ] {
            SendMessageW(handle, 0x0030, font as WPARAM, TRUE as LPARAM);
        }
    }

    fn browse_for_game_path(&mut self) {
        if let Some(path) = browse_folder(self.hwnd, self.lang, tr(self.lang, Text::SelectFolderTitle)) {
            self.set_game_path(path);
        }
    }

    fn install_or_update_mod(&mut self) {
        let Some(game_path) = self.game_path.clone() else {
            return;
        };

        self.set_busy(true, Some(tr(self.lang, Text::FetchingVersions).to_string()));
        match get_releases() {
            Ok(releases) => {
                let releases: Vec<_> = releases.into_iter().filter(has_mod_asset).collect();
                if releases.is_empty() {
                    message_box(self.hwnd, self.lang, Text::NoVersionsTitle, tr(self.lang, Text::NoVersions), MB_OK | MB_ICONINFORMATION);
                    self.set_busy(false, None);
                    return;
                }

                self.set_busy(false, None);
                let Some(release) = select_release_dialog(self.hwnd, self.lang, &releases) else {
                    return;
                };
                if !show_changelog_dialog(self.hwnd, self.lang, &release) {
                    return;
                }
                let Some(asset) = release.assets.iter().find(|asset| is_mod_asset(asset)).cloned() else {
                    return;
                };
                self.start_job(Job::Install { game_path, release, asset });
            }
            Err(err) => {
                message_box(self.hwnd, self.lang, Text::InstallError, &err.to_string(), MB_OK | MB_ICONERROR);
                self.set_busy(false, None);
            }
        }
    }

    fn uninstall_mod(&mut self) {
        let Some(game_path) = self.game_path.clone() else {
            return;
        };

        let answer = message_box(
            self.hwnd,
            self.lang,
            Text::RemoveQuestionTitle,
            tr(self.lang, Text::RemoveQuestion),
            MB_YESNO | MB_ICONQUESTION,
        );
        if answer != 6 {
            return;
        }

        match uninstall_package(&game_path, MOD_NAME) {
            Ok(()) => {
                message_box(self.hwnd, self.lang, Text::Done, tr(self.lang, Text::Removed), MB_OK | MB_ICONINFORMATION);
                self.set_game_path(game_path);
            }
            Err(err) => {
                message_box(self.hwnd, self.lang, Text::RemoveError, &err.to_string(), MB_OK | MB_ICONERROR);
            }
        }
    }

    fn install_russian_localization(&mut self) {
        let Some(game_path) = self.game_path.clone() else {
            return;
        };
        self.start_job(Job::InstallRussian { game_path });
    }

    fn start_job(&mut self, job: Job) {
        let initial = match job {
            Job::Install { .. } => tr(self.lang, Text::DownloadingVersion),
            Job::InstallRussian { .. } => tr(self.lang, Text::PreparingRussian),
        };
        self.set_busy(true, Some(initial.to_string()));

        let tx = self.tx.clone();
        let hwnd = self.hwnd as isize;
        let lang = self.lang;
        thread::spawn(move || {
            let result = match job {
                Job::Install { game_path, release, asset } => worker_install_mod(lang, &tx, hwnd, &game_path, &release, &asset),
                Job::InstallRussian { game_path } => worker_install_russian(lang, &tx, hwnd, &game_path),
            };
            send_message(&tx, hwnd, WorkerMessage::Finished(result.map_err(|err| err.to_string())));
        });
    }

    fn process_worker_messages(&mut self) {
        while let Ok(message) = self.rx.try_recv() {
            match message {
                WorkerMessage::Status(status) => set_text(self.status_label, &status),
                WorkerMessage::Progress(value) => unsafe {
                    SendMessageW(self.progress, PBM_SETPOS, value as WPARAM, 0);
                },
                WorkerMessage::Finished(result) => {
                    self.set_busy(false, None);
                    match result {
                        Ok(WorkerResult::Installed) => {
                            message_box(self.hwnd, self.lang, Text::Done, tr(self.lang, Text::ModInstalled), MB_OK | MB_ICONINFORMATION);
                        }
                        Ok(WorkerResult::RussianInstalled) => {
                            message_box(self.hwnd, self.lang, Text::Done, tr(self.lang, Text::RussianInstalled), MB_OK | MB_ICONINFORMATION);
                        }
                        Err(err) => {
                            message_box(self.hwnd, self.lang, Text::InstallError, &err, MB_OK | MB_ICONERROR);
                        }
                    }
                    if let Some(path) = self.game_path.clone() {
                        self.set_game_path(path);
                    }
                }
            }
        }
    }

    fn set_game_path(&mut self, path: PathBuf) {
        self.game_path = Some(path.clone());
        set_text(self.path_box, &path.display().to_string());

        let state = get_state(&path);
        let mod_state = if state.is_mod_installed {
            format!("{} {}.", tr(self.lang, Text::ModInstalledState), format_version(self.lang, &state.installed_version))
        } else {
            tr(self.lang, Text::ModNotInstalledState).to_string()
        };
        let bepin_state = if state.has_bepinex { tr(self.lang, Text::BepInExFound) } else { tr(self.lang, Text::BepInExMissing) };
        let ru_state = if state.is_russian_localization_installed { tr(self.lang, Text::RussianFound) } else { tr(self.lang, Text::RussianMissing) };
        let status = if state.is_valid_game {
            format!("{} {} {} {}", tr(self.lang, Text::GameFolderSelected), bepin_state, mod_state, ru_state)
        } else {
            tr(self.lang, Text::InvalidGameFolder).to_string()
        };

        set_text(self.status_label, &status);
        self.refresh_buttons();
    }

    fn refresh_buttons(&mut self) {
        let valid = self.game_path.as_ref().map(|path| is_game_directory(path)).unwrap_or(false);
        let mod_installed = self.game_path.as_ref().map(|path| get_state(path).is_mod_installed).unwrap_or(false);
        enable(self.browse_button, !self.busy);
        enable(self.install_button, !self.busy && valid && !mod_installed);
        enable(self.update_button, !self.busy && valid && mod_installed);
        enable(self.remove_button, !self.busy && valid && mod_installed);
        enable(self.install_ru_button, !self.busy && valid);
    }

    fn set_busy(&mut self, busy: bool, status: Option<String>) {
        self.busy = busy;
        if let Some(status) = status {
            set_text(self.status_label, &status);
        }
        if !busy {
            unsafe {
                SendMessageW(self.progress, PBM_SETPOS, 0, 0);
            }
        }
        self.refresh_buttons();
    }
}

unsafe fn create_label(parent: HWND, text: &str, x: i32, y: i32, width: i32, height: i32) -> HWND {
    create_control("STATIC", text, WS_CHILD | WS_VISIBLE, 0, x, y, width, height, parent, 0)
}

unsafe fn create_button(parent: HWND, text: &str, x: i32, y: i32, width: i32, height: i32, id: i32) -> HWND {
    create_control("BUTTON", text, WS_CHILD | WS_VISIBLE | WS_TABSTOP, 0, x, y, width, height, parent, id)
}

unsafe fn create_control(
    class_name: &str,
    text: &str,
    style: u32,
    ex_style: u32,
    x: i32,
    y: i32,
    width: i32,
    height: i32,
    parent: HWND,
    id: i32,
) -> HWND {
    CreateWindowExW(
        ex_style as WINDOW_EX_STYLE,
        wide(class_name).as_ptr(),
        wide(text).as_ptr(),
        style,
        x,
        y,
        width,
        height,
        parent,
        id as HMENU,
        ptr::null_mut(),
        ptr::null_mut(),
    )
}

fn set_text(hwnd: HWND, text: &str) {
    unsafe {
        SetWindowTextW(hwnd, wide(text).as_ptr());
    }
}

fn enable(hwnd: HWND, enabled: bool) {
    unsafe {
        EnableWindow(hwnd, enabled as BOOL);
    }
}

fn message_box(parent: HWND, lang: Lang, title: Text, content: &str, flags: u32) -> i32 {
    unsafe {
        MessageBoxW(
            parent,
            wide(content).as_ptr(),
            wide(tr(lang, title)).as_ptr(),
            flags,
        )
    }
}

fn browse_folder(owner: HWND, _lang: Lang, title: &str) -> Option<PathBuf> {
    unsafe {
        let title_w = wide(title);
        let mut bi = BROWSEINFOW {
            hwndOwner: owner,
            lpszTitle: title_w.as_ptr(),
            ulFlags: BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE,
            ..std::mem::zeroed()
        };
        let pidl = SHBrowseForFolderW(&mut bi);
        if pidl.is_null() {
            return None;
        }

        let mut path = [0u16; MAX_PATH as usize];
        let ok = SHGetPathFromIDListW(pidl, path.as_mut_ptr());
        CoTaskMemFree(pidl.cast::<c_void>());
        if ok == 0 {
            return None;
        }
        Some(PathBuf::from(from_wide_z(&path)))
    }
}

struct ModalState {
    done: Cell<bool>,
    accepted: Cell<bool>,
    selection: Cell<Option<usize>>,
    list: Cell<HWND>,
    changelog_edit: Cell<HWND>,
    ok_button: Cell<HWND>,
    cancel_button: Cell<HWND>,
}

fn select_release_dialog(owner: HWND, lang: Lang, releases: &[ReleaseInfo]) -> Option<ReleaseInfo> {
    let state = ModalState {
        done: Cell::new(false),
        accepted: Cell::new(false),
        selection: Cell::new(None),
        list: Cell::new(ptr::null_mut()),
        changelog_edit: Cell::new(ptr::null_mut()),
        ok_button: Cell::new(ptr::null_mut()),
        cancel_button: Cell::new(ptr::null_mut()),
    };
    let hwnd = create_modal_window(owner, lang, tr(lang, Text::VersionWindowTitle), 560, 380, &state);
    unsafe {
        create_label(hwnd, tr(lang, Text::VersionLabel), 12, 12, 520, 24);
        state.list.set(create_control(
            "LISTBOX",
            "",
            WS_CHILD | WS_VISIBLE | WS_BORDER | WS_TABSTOP | (LBS_NOTIFY as u32) | WS_VSCROLL,
            WS_EX_CLIENTEDGE,
            12,
            40,
            520,
            240,
            hwnd,
            ID_VERSION_LIST,
        ));
        for release in releases {
            SendMessageW(state.list.get(), LB_ADDSTRING, 0, wide(&release.display_name()).as_ptr() as LPARAM);
        }
        SendMessageW(state.list.get(), LB_SETCURSEL, 0, 0);
        state
            .ok_button
            .set(create_button(hwnd, tr(lang, Text::Continue), 300, 295, 110, 32, ID_OK));
        state
            .cancel_button
            .set(create_button(hwnd, tr(lang, Text::Cancel), 422, 295, 110, 32, ID_CANCEL));
        ShowWindow(hwnd, SW_SHOW);
        EnableWindow(owner, 0);
        SetFocus(state.list.get());
        run_modal_loop(owner, &state);
        EnableWindow(owner, 1);
        DestroyWindow(hwnd);
    }

    if state.accepted.get() {
        state.selection.get().and_then(|index| releases.get(index).cloned())
    } else {
        None
    }
}

fn show_changelog_dialog(owner: HWND, lang: Lang, release: &ReleaseInfo) -> bool {
    let state = ModalState {
        done: Cell::new(false),
        accepted: Cell::new(false),
        selection: Cell::new(None),
        list: Cell::new(ptr::null_mut()),
        changelog_edit: Cell::new(ptr::null_mut()),
        ok_button: Cell::new(ptr::null_mut()),
        cancel_button: Cell::new(ptr::null_mut()),
    };
    let title = format!("{} {}", tr(lang, Text::ChangelogTitle), release.tag_name);
    let hwnd = create_modal_window(owner, lang, &title, 700, 520, &state);
    let text = if release.changelog.trim().is_empty() {
        tr(lang, Text::NoChangelog).to_string()
    } else {
        windows_multiline_text(&release.changelog)
    };

    unsafe {
        state.changelog_edit.set(create_control(
            "EDIT",
            &text,
            WS_CHILD
                | WS_VISIBLE
                | WS_BORDER
                | WS_TABSTOP
                | WS_VSCROLL
                | ES_MULTILINE
                | ES_AUTOVSCROLL
                | ES_READONLY,
            WS_EX_CLIENTEDGE,
            12,
            12,
            660,
            400,
            hwnd,
            0,
        ));
        state
            .ok_button
            .set(create_button(hwnd, tr(lang, Text::Continue), 440, 426, 110, 32, ID_OK));
        state
            .cancel_button
            .set(create_button(hwnd, tr(lang, Text::Cancel), 562, 426, 110, 32, ID_CANCEL));
        ShowWindow(hwnd, SW_SHOW);
        EnableWindow(owner, 0);
        SendMessageW(state.changelog_edit.get(), EM_SETSEL, 0, 0);
        SendMessageW(state.changelog_edit.get(), EM_SCROLLCARET, 0, 0);
        SetFocus(state.changelog_edit.get());
        run_modal_loop(owner, &state);
        EnableWindow(owner, 1);
        DestroyWindow(hwnd);
    }

    state.accepted.get()
}

fn create_modal_window(owner: HWND, _lang: Lang, title: &str, width: i32, height: i32, state: &ModalState) -> HWND {
    unsafe {
        let hinstance = GetModuleHandleW(ptr::null());
        let class_name = wide("AccessTheObeliskInstallerModal");
        let wc = WNDCLASSW {
            lpfnWndProc: Some(modal_wnd_proc),
            hInstance: hinstance,
            lpszClassName: class_name.as_ptr(),
            hCursor: LoadCursorW(ptr::null_mut(), IDC_ARROW),
            ..std::mem::zeroed()
        };
        RegisterClassW(&wc);
        CreateWindowExW(
            0,
            class_name.as_ptr(),
            wide(title).as_ptr(),
            WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            width,
            height,
            owner,
            ptr::null_mut(),
            hinstance,
            (state as *const ModalState as *mut ModalState).cast(),
        )
    }
}

extern "system" fn modal_wnd_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe {
        if msg == WM_NCCREATE {
            let create = lparam as *const CREATESTRUCTW;
            SetWindowLongPtrW(hwnd, GWLP_USERDATA, (*create).lpCreateParams as isize);
            return DefWindowProcW(hwnd, msg, wparam, lparam);
        }
        let state = GetWindowLongPtrW(hwnd, GWLP_USERDATA) as *mut ModalState;
        if state.is_null() {
            return DefWindowProcW(hwnd, msg, wparam, lparam);
        }
        let state = &*state;
        match msg {
            WM_COMMAND => {
                let id = (wparam & 0xffff) as i32;
                if id == ID_OK {
                    state.accepted.set(true);
                    if !state.list.get().is_null() {
                        let index = SendMessageW(state.list.get(), LB_GETCURSEL, 0, 0);
                        if index >= 0 {
                            state.selection.set(Some(index as usize));
                        }
                    }
                    state.done.set(true);
                } else if id == ID_CANCEL {
                    state.accepted.set(false);
                    state.done.set(true);
                }
                0
            }
            WM_CLOSE => {
                state.accepted.set(false);
                state.done.set(true);
                0
            }
            _ => DefWindowProcW(hwnd, msg, wparam, lparam),
        }
    }
}

unsafe fn run_modal_loop(owner: HWND, state: &ModalState) {
    let mut msg: MSG = std::mem::zeroed();
    while !state.done.get() && GetMessageW(&mut msg, ptr::null_mut(), 0, 0) > 0 {
        if msg.message == WM_KEYDOWN_MSG
            && msg.wParam == VK_TAB_KEY
            && !state.changelog_edit.get().is_null()
            && msg.hwnd == state.changelog_edit.get()
        {
            let target = if GetKeyState(VK_SHIFT_KEY) < 0 {
                state.cancel_button.get()
            } else {
                state.ok_button.get()
            };
            if !target.is_null() {
                SetFocus(target);
                continue;
            }
        }
        if IsDialogMessageW(GetAncestor(msg.hwnd, GA_ROOT), &mut msg) == 0 {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
    }
    SetFocus(owner);
}

fn system_lang() -> Lang {
    unsafe {
        let lang_id = GetUserDefaultUILanguage();
        let primary = lang_id & 0x03ff;
        if primary == 0x19 {
            Lang::Ru
        } else {
            Lang::En
        }
    }
}

fn worker_install_mod(
    lang: Lang,
    tx: &Sender<WorkerMessage>,
    hwnd: isize,
    game_path: &Path,
    release: &ReleaseInfo,
    asset: &ReleaseAssetInfo,
) -> Result<WorkerResult> {
    let temp_file = std::env::temp_dir().join(&asset.name);
    send_message(tx, hwnd, WorkerMessage::Status(tr(lang, Text::DownloadingVersion).to_string()));
    download_file(&asset.download_url, &temp_file, tx, hwnd)?;
    send_message(tx, hwnd, WorkerMessage::Status(tr(lang, Text::InstallingMod).to_string()));
    install_package(game_path, &temp_file, MOD_NAME, release.tag_name.trim_start_matches(['v', 'V']))?;
    if !has_bepinex_runtime(game_path) {
        send_message(
            tx,
            hwnd,
            WorkerMessage::Status(tr(lang, Text::InstallingBepInEx).to_string()),
        );
        install_across_the_obelisk_bepinex_pack(game_path, tx, hwnd)?;
    }
    Ok(WorkerResult::Installed)
}

fn worker_install_russian(
    lang: Lang,
    tx: &Sender<WorkerMessage>,
    hwnd: isize,
    game_path: &Path,
) -> Result<WorkerResult> {
    send_message(tx, hwnd, WorkerMessage::Status(tr(lang, Text::PreparingRussian).to_string()));
    let zip_path = if Path::new(LOCAL_RUSSIAN_LOCALIZATION_PATH).exists() {
        PathBuf::from(LOCAL_RUSSIAN_LOCALIZATION_PATH)
    } else {
        let releases = get_releases()?;
        let asset = releases
            .iter()
            .flat_map(|release| release.assets.iter())
            .find(|asset| asset.name.eq_ignore_ascii_case(RUSSIAN_LOCALIZATION_ASSET))
            .cloned()
            .ok_or_else(|| anyhow!("Archive {} was not found in GitHub Releases or at {}.", RUSSIAN_LOCALIZATION_ASSET, LOCAL_RUSSIAN_LOCALIZATION_PATH))?;
        let temp_file = std::env::temp_dir().join(&asset.name);
        download_file(&asset.download_url, &temp_file, tx, hwnd)?;
        temp_file
    };

    install_russian_localization(game_path, &zip_path)?;
    Ok(WorkerResult::RussianInstalled)
}

fn send_message(tx: &Sender<WorkerMessage>, hwnd: isize, message: WorkerMessage) {
    let _ = tx.send(message);
    unsafe {
        PostMessageW(hwnd as HWND, WM_WORKER, 0, 0);
    }
}

fn get_releases() -> Result<Vec<ReleaseInfo>> {
    let url = format!("https://api.github.com/repos/{}/{}/releases", GITHUB_OWNER, GITHUB_REPOSITORY);
    let agent = http_agent()?;
    let response = agent
        .get(&url)
        .set("User-Agent", &format!("AccessTheObelisk.Installer/{}", INSTALLER_VERSION))
        .set("Accept", "application/vnd.github+json")
        .call();

    match response {
        Ok(response) => {
            let releases: Vec<GitHubRelease> = response
                .into_json()
                .context("Failed to parse GitHub Releases response")?;
            Ok(releases
                .into_iter()
                .map(|release| ReleaseInfo {
                    tag_name: release.tag_name.clone(),
                    name: release.name.unwrap_or_else(|| release.tag_name.clone()),
                    changelog: release.body.unwrap_or_default(),
                    assets: release
                        .assets
                        .into_iter()
                        .map(|asset| ReleaseAssetInfo {
                            name: asset.name,
                            download_url: asset.browser_download_url,
                        })
                        .collect(),
                })
                .collect())
        }
        Err(api_error) => get_releases_from_atom().with_context(|| {
            format!(
                "GitHub API request failed: {}. The public release feed fallback also failed",
                api_error
            )
        }),
    }
}

fn get_releases_from_atom() -> Result<Vec<ReleaseInfo>> {
    let url = format!(
        "https://github.com/{}/{}/releases.atom",
        GITHUB_OWNER, GITHUB_REPOSITORY
    );
    let response = http_agent()?
        .get(&url)
        .set(
            "User-Agent",
            &format!("AccessTheObelisk.Installer/{}", INSTALLER_VERSION),
        )
        .call()
        .context("Failed to request the public GitHub release feed")?;
    let mut atom = String::new();
    response
        .into_reader()
        .read_to_string(&mut atom)
        .context("Failed to read the public GitHub release feed")?;
    parse_atom_releases(&atom)
}

fn parse_atom_releases(atom: &str) -> Result<Vec<ReleaseInfo>> {
    let mut releases = Vec::new();
    for entry in atom.split("<entry>").skip(1) {
        let Some(entry) = entry.split("</entry>").next() else {
            continue;
        };
        let title = xml_decode(extract_element(entry, "title").unwrap_or_default());
        let content = xml_decode(extract_element(entry, "content").unwrap_or_default());
        let Some(tag_name) = extract_release_tag(entry) else {
            continue;
        };

        let mod_asset_name = format!("AccessTheObelisk-{}.zip", tag_name);
        let base_url = format!(
            "https://github.com/{}/{}/releases/download/{}",
            GITHUB_OWNER, GITHUB_REPOSITORY, tag_name
        );
        releases.push(ReleaseInfo {
            tag_name: tag_name.clone(),
            name: title,
            changelog: html_to_text(&content),
            assets: vec![
                ReleaseAssetInfo {
                    name: mod_asset_name.clone(),
                    download_url: format!("{}/{}", base_url, mod_asset_name),
                },
                ReleaseAssetInfo {
                    name: RUSSIAN_LOCALIZATION_ASSET.to_string(),
                    download_url: format!("{}/{}", base_url, RUSSIAN_LOCALIZATION_ASSET),
                },
            ],
        });
    }

    if releases.is_empty() {
        bail!("The public GitHub release feed did not contain any releases");
    }
    Ok(releases)
}

fn extract_element<'a>(source: &'a str, element: &str) -> Option<&'a str> {
    let open_start = source.find(&format!("<{}", element))?;
    let content_start = source[open_start..].find('>')? + open_start + 1;
    let close_start = source[content_start..].find(&format!("</{}>", element))? + content_start;
    Some(&source[content_start..close_start])
}

fn extract_release_tag(entry: &str) -> Option<String> {
    let marker = "/releases/tag/";
    let start = entry.find(marker)? + marker.len();
    let tail = &entry[start..];
    let end = tail.find(['"', '&', '<']).unwrap_or(tail.len());
    let tag = xml_decode(&tail[..end]);
    if tag.trim().is_empty() {
        None
    } else {
        Some(tag)
    }
}

fn xml_decode(value: &str) -> String {
    value
        .replace("&lt;", "<")
        .replace("&gt;", ">")
        .replace("&quot;", "\"")
        .replace("&#39;", "'")
        .replace("&apos;", "'")
        .replace("&amp;", "&")
}

fn html_to_text(value: &str) -> String {
    let prepared = value
        .replace("<br>", "\n")
        .replace("<br/>", "\n")
        .replace("<br />", "\n")
        .replace("</p>", "\n")
        .replace("</li>", "\n");
    let mut result = String::new();
    let mut inside_tag = false;
    for ch in prepared.chars() {
        match ch {
            '<' => inside_tag = true,
            '>' => inside_tag = false,
            _ if !inside_tag => result.push(ch),
            _ => {}
        }
    }
    result
        .lines()
        .map(str::trim)
        .filter(|line| !line.is_empty())
        .collect::<Vec<_>>()
        .join("\r\n")
}

fn windows_multiline_text(value: &str) -> String {
    value.replace("\r\n", "\n").replace('\r', "\n").replace('\n', "\r\n")
}

fn download_file(url: &str, target_path: &Path, tx: &Sender<WorkerMessage>, hwnd: isize) -> Result<()> {
    let response = http_agent()?
        .get(url)
        .set("User-Agent", &format!("AccessTheObelisk.Installer/{}", INSTALLER_VERSION))
        .call()
        .context("Failed to download file")?;
    let total = response.header("Content-Length").and_then(|value| value.parse::<u64>().ok()).unwrap_or(0);
    let mut input = response.into_reader();
    let mut output = File::create(target_path).with_context(|| format!("Failed to create {}", target_path.display()))?;
    let mut buffer = [0u8; 81920];
    let mut copied = 0u64;
    loop {
        let read = input.read(&mut buffer).context("Failed to read download stream")?;
        if read == 0 {
            break;
        }
        output.write_all(&buffer[..read]).context("Failed to write downloaded file")?;
        copied += read as u64;
        if total > 0 {
            send_message(tx, hwnd, WorkerMessage::Progress(((copied.saturating_mul(100)) / total).min(100) as u32));
        }
    }
    send_message(tx, hwnd, WorkerMessage::Progress(100));
    Ok(())
}

fn http_agent() -> Result<ureq::Agent> {
    let tls = ureq::native_tls::TlsConnector::new()
        .context("Failed to initialize Windows TLS")?;
    Ok(ureq::builder()
        .tls_connector(Arc::new(tls))
        .redirects(10)
        .build())
}

fn install_across_the_obelisk_bepinex_pack(
    game_path: &Path,
    tx: &Sender<WorkerMessage>,
    hwnd: isize,
) -> Result<()> {
    let asset_name = format!(
        "BepInExPack_AcrossTheObelisk_{}.zip",
        BEPINEX_PACK_VERSION
    );
    let url = format!(
        "https://thunderstore.io/package/download/BepInEx/BepInExPack_AcrossTheObelisk/{}/",
        BEPINEX_PACK_VERSION
    );
    let temp_file = std::env::temp_dir().join(&asset_name);
    download_file(&url, &temp_file, tx, hwnd)?;
    extract_bepinex_pack_to_game(game_path, &temp_file)
}

fn extract_bepinex_pack_to_game(game_path: &Path, zip_path: &Path) -> Result<()> {
    let file = File::open(zip_path)
        .with_context(|| format!("Failed to open archive {}", zip_path.display()))?;
    let mut archive = ZipArchive::new(file).context("Failed to read archive")?;
    for index in 0..archive.len() {
        let mut entry = archive.by_index(index).context("Failed to open archive entry")?;
        if entry.name().ends_with('/') || entry.name().ends_with('\\') {
            continue;
        }
        let normalized = normalize_relative_path(entry.name());
        let Some(relative_path) = normalized.strip_prefix(BEPINEX_PACK_PREFIX) else {
            continue;
        };
        if relative_path.is_empty() {
            continue;
        }
        let target_path = safe_target_path(game_path, relative_path)?;
        if let Some(parent) = target_path.parent() {
            fs::create_dir_all(parent)
                .with_context(|| format!("Failed to create {}", parent.display()))?;
        }
        let mut output = File::create(&target_path)
            .with_context(|| format!("Failed to create {}", target_path.display()))?;
        io::copy(&mut entry, &mut output)
            .with_context(|| format!("Failed to extract {}", entry.name()))?;
    }
    Ok(())
}

fn has_mod_asset(release: &ReleaseInfo) -> bool {
    release.assets.iter().any(is_mod_asset)
}

fn is_mod_asset(asset: &ReleaseAssetInfo) -> bool {
    asset.name.to_ascii_lowercase().starts_with("accesstheobelisk-v")
        && asset.name.to_ascii_lowercase().ends_with(".zip")
}

fn find_game_directory() -> Option<PathBuf> {
    game_directory_candidates().into_iter().find(|candidate| is_game_directory(candidate))
}

fn game_directory_candidates() -> Vec<PathBuf> {
    let mut candidates = vec![PathBuf::from(r"D:\Across.the.Obelisk.v1.7.5.1")];
    for steam_library in steam_libraries() {
        candidates.push(steam_library.join(r"steamapps\common\Across the Obelisk"));
        candidates.push(steam_library.join(r"steamapps\common\AcrossTheObelisk"));
    }
    candidates.push(PathBuf::from(r"C:\Program Files (x86)\Steam\steamapps\common\Across the Obelisk"));
    candidates.push(PathBuf::from(r"C:\Program Files\Steam\steamapps\common\Across the Obelisk"));
    candidates.push(PathBuf::from(r"D:\SteamLibrary\steamapps\common\Across the Obelisk"));
    candidates.push(PathBuf::from(r"E:\SteamLibrary\steamapps\common\Across the Obelisk"));
    candidates
}

fn steam_libraries() -> Vec<PathBuf> {
    let Some(steam_path) = steam_path_from_registry() else {
        return Vec::new();
    };
    let mut libraries = vec![steam_path.clone()];
    let library_file = steam_path.join(r"steamapps\libraryfolders.vdf");
    let Ok(content) = fs::read_to_string(library_file) else {
        return libraries;
    };
    for line in content.lines() {
        let trimmed = line.trim();
        if !trimmed.to_ascii_lowercase().contains("\"path\"") {
            continue;
        }
        let parts: Vec<_> = trimmed.split('"').filter(|part| !part.is_empty()).collect();
        if parts.len() >= 4 {
            libraries.push(PathBuf::from(parts[3].replace(r"\\", r"\")));
        }
    }
    libraries
}

fn steam_path_from_registry() -> Option<PathBuf> {
    let hkcu = RegKey::predef(HKEY_CURRENT_USER);
    let key = hkcu.open_subkey(r"Software\Valve\Steam").ok()?;
    let value: String = key.get_value("SteamPath").ok()?;
    Some(PathBuf::from(value))
}

fn is_game_directory(path: &Path) -> bool {
    path.join("AcrossTheObelisk.exe").is_file() && path.join("AcrossTheObelisk_Data").is_dir()
}

fn has_bepinex_runtime(game_path: &Path) -> bool {
    game_path.join("winhttp.dll").is_file()
        && game_path.join("doorstop_config.ini").is_file()
        && game_path.join(r"BepInEx\core\BepInEx.dll").is_file()
        && game_path.join(r"BepInEx\config\BepInEx.cfg").is_file()
}

fn get_state(game_path: &Path) -> GameInstallState {
    let package = read_installed_package(game_path, MOD_NAME).ok().flatten();
    let has_current_dll = game_path.join(r"BepInEx\plugins\AccessTheObelisk\AccessTheObelisk.dll").is_file();
    let has_legacy_dll = game_path.join(r"BepInEx\plugins\AccessTheObelisk.dll").is_file();
    let installed_version = package.as_ref().map(|package| package.version.clone()).unwrap_or_default();
    GameInstallState {
        is_valid_game: is_game_directory(game_path),
        has_bepinex: has_bepinex_runtime(game_path),
        is_mod_installed: package.is_some() || has_current_dll || has_legacy_dll,
        installed_version,
        is_russian_localization_installed: game_path.join(r"BepInEx\plugins\RussianTranslation\RussianTranslation.dll").is_file(),
    }
}

fn install_package(game_path: &Path, zip_path: &Path, fallback_package_id: &str, fallback_version: &str) -> Result<()> {
    let file = File::open(zip_path).with_context(|| format!("Failed to open archive {}", zip_path.display()))?;
    let mut archive = ZipArchive::new(file).context("Failed to read archive")?;
    let manifest = read_manifest(&mut archive)?.unwrap_or_else(|| build_manifest_from_archive(&mut archive, fallback_package_id, fallback_version));
    if manifest.package_id.eq_ignore_ascii_case(MOD_NAME) {
        remove_legacy_mod_files(game_path)?;
        uninstall_package(game_path, MOD_NAME)?;
    }
    let mut installed_files = Vec::new();
    for package_file in &manifest.files {
        let Some(index) = find_archive_entry(&mut archive, &package_file.path) else {
            continue;
        };
        let mut entry = archive.by_index(index).context("Failed to open archive entry")?;
        if entry.name().ends_with('/') || entry.name().ends_with('\\') {
            continue;
        }
        let target_path = safe_target_path(game_path, &package_file.path)?;
        if let Some(parent) = target_path.parent() {
            fs::create_dir_all(parent).with_context(|| format!("Failed to create {}", parent.display()))?;
        }
        let mut output = File::create(&target_path).with_context(|| format!("Failed to create {}", target_path.display()))?;
        io::copy(&mut entry, &mut output).with_context(|| format!("Failed to extract {}", package_file.path))?;
        if !is_shared_bepinex_file(&package_file.path) {
            installed_files.push(normalize_relative_path(&package_file.path));
        }
    }
    write_installed_package(game_path, &InstalledPackage { package_id: manifest.package_id, version: manifest.version, files: installed_files })
}

fn install_russian_localization(game_path: &Path, zip_path: &Path) -> Result<()> {
    let file = File::open(zip_path).with_context(|| format!("Failed to open archive {}", zip_path.display()))?;
    let mut archive = ZipArchive::new(file).context("Failed to read archive")?;
    let manifest = build_manifest_from_archive(&mut archive, "RussianTranslation", "1.2.1");
    remove_known_russian_localization_files(game_path)?;
    let mut installed_files = Vec::new();
    for package_file in &manifest.files {
        if !should_install_russian_localization_entry(&package_file.path) {
            continue;
        }
        let Some(index) = find_archive_entry(&mut archive, &package_file.path) else {
            continue;
        };
        let mut entry = archive.by_index(index).context("Failed to open archive entry")?;
        if entry.name().ends_with('/') || entry.name().ends_with('\\') {
            continue;
        }
        let target_path = safe_target_path(game_path, &package_file.path)?;
        if let Some(parent) = target_path.parent() {
            fs::create_dir_all(parent).with_context(|| format!("Failed to create {}", parent.display()))?;
        }
        let mut output = File::create(&target_path).with_context(|| format!("Failed to create {}", target_path.display()))?;
        io::copy(&mut entry, &mut output).with_context(|| format!("Failed to extract {}", package_file.path))?;
        installed_files.push(normalize_relative_path(&package_file.path));
    }
    write_installed_package(game_path, &InstalledPackage { package_id: "RussianTranslation".to_string(), version: "1.2.1".to_string(), files: installed_files })
}

fn uninstall_package(game_path: &Path, package_id: &str) -> Result<()> {
    let Some(package) = read_installed_package(game_path, package_id)? else {
        if package_id.eq_ignore_ascii_case(MOD_NAME) {
            remove_known_mod_files(game_path)?;
        }
        return Ok(());
    };
    let mut files = package.files.clone();
    files.sort_by_key(|file| std::cmp::Reverse(file.len()));
    for relative_file in &files {
        if package_id.eq_ignore_ascii_case(MOD_NAME)
            && is_shared_bepinex_file(relative_file)
        {
            continue;
        }
        let target_path = safe_target_path(game_path, relative_file)?;
        if target_path.is_file() {
            fs::remove_file(&target_path).with_context(|| format!("Failed to delete {}", target_path.display()))?;
        }
    }
    delete_empty_directories(game_path, &files)?;
    let record = install_record_path(game_path, package_id);
    if record.exists() {
        fs::remove_file(record).context("Failed to remove install record")?;
    }
    Ok(())
}

fn read_manifest(archive: &mut ZipArchive<File>) -> Result<Option<PackageManifest>> {
    let Ok(mut entry) = archive.by_name("manifest.json") else {
        return Ok(None);
    };
    let mut json = String::new();
    entry.read_to_string(&mut json).context("Failed to read manifest.json")?;
    Ok(Some(serde_json::from_str(&json).context("Failed to parse manifest.json")?))
}

fn build_manifest_from_archive(archive: &mut ZipArchive<File>, package_id: &str, version: &str) -> PackageManifest {
    let mut files = Vec::new();
    for index in 0..archive.len() {
        if let Ok(entry) = archive.by_index(index) {
            let path = normalize_relative_path(entry.name());
            if path.is_empty() || path.eq_ignore_ascii_case("manifest.json") || entry.name().ends_with('/') || entry.name().ends_with('\\') {
                continue;
            }
            files.push(PackageFile { path, sha256: String::new() });
        }
    }
    PackageManifest { package_id: package_id.to_string(), name: package_id.to_string(), version: version.to_string(), files }
}

fn find_archive_entry(archive: &mut ZipArchive<File>, relative_path: &str) -> Option<usize> {
    let normalized = normalize_relative_path(relative_path);
    for index in 0..archive.len() {
        let Ok(entry) = archive.by_index(index) else {
            continue;
        };
        if normalize_relative_path(entry.name()).eq_ignore_ascii_case(&normalized) {
            return Some(index);
        }
    }
    None
}

fn should_install_russian_localization_entry(relative_path: &str) -> bool {
    normalize_relative_path(relative_path).to_ascii_lowercase().starts_with("bepinex/plugins/russiantranslation/")
}

fn is_shared_bepinex_file(relative_path: &str) -> bool {
    let normalized = normalize_relative_path(relative_path).to_ascii_lowercase();
    normalized.starts_with("bepinex/core/")
        || normalized == "bepinex/config/bepinex.cfg"
        || normalized.starts_with("doorstop_libs/")
        || matches!(
            normalized.as_str(),
            "winhttp.dll"
                | "doorstop_config.ini"
                | "run_bepinex.sh"
                | ".doorstop_version"
                | "changelog.txt"
        )
}

fn remove_legacy_mod_files(game_path: &Path) -> Result<()> {
    let legacy_dll = game_path.join(r"BepInEx\plugins\AccessTheObelisk.dll");
    if legacy_dll.is_file() {
        fs::remove_file(&legacy_dll).with_context(|| format!("Failed to delete {}", legacy_dll.display()))?;
    }
    Ok(())
}

fn remove_known_mod_files(game_path: &Path) -> Result<()> {
    remove_legacy_mod_files(game_path)?;
    let plugin_dir = game_path.join(r"BepInEx\plugins\AccessTheObelisk");
    if plugin_dir.is_dir() {
        fs::remove_dir_all(&plugin_dir)
            .with_context(|| format!("Failed to delete {}", plugin_dir.display()))?;
    }
    let prism = game_path.join("prism.dll");
    if prism.is_file() {
        fs::remove_file(&prism)
            .with_context(|| format!("Failed to delete {}", prism.display()))?;
    }
    Ok(())
}

fn remove_known_russian_localization_files(game_path: &Path) -> Result<()> {
    if install_record_path(game_path, "RussianTranslation").exists() {
        return Ok(());
    }
    let plugin_dir = game_path.join(r"BepInEx\plugins\RussianTranslation");
    if plugin_dir.is_dir() {
        fs::remove_dir_all(&plugin_dir).with_context(|| format!("Failed to delete {}", plugin_dir.display()))?;
    }
    Ok(())
}

fn read_installed_package(game_path: &Path, package_id: &str) -> Result<Option<InstalledPackage>> {
    let path = install_record_path(game_path, package_id);
    if !path.exists() {
        return Ok(None);
    }
    let json = fs::read_to_string(&path).with_context(|| format!("Failed to read {}", path.display()))?;
    Ok(Some(serde_json::from_str(&json).with_context(|| format!("Failed to parse {}", path.display()))?))
}

fn write_installed_package(game_path: &Path, package: &InstalledPackage) -> Result<()> {
    let path = install_record_path(game_path, &package.package_id);
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).with_context(|| format!("Failed to create {}", parent.display()))?;
    }
    let json = serde_json::to_string_pretty(package).context("Failed to serialize install record")?;
    fs::write(&path, json).with_context(|| format!("Failed to write {}", path.display()))
}

fn install_record_path(game_path: &Path, package_id: &str) -> PathBuf {
    game_path.join(r"BepInEx\plugins\AccessTheObelisk").join(format!("{}.install.json", package_id))
}

fn safe_target_path(game_path: &Path, relative_path: &str) -> Result<PathBuf> {
    let normalized = normalize_relative_path(relative_path);
    if normalized.is_empty() || normalized.contains(':') || normalized.split('/').any(|part| part == ".." || part.is_empty()) {
        bail!("Unsafe package path: {}", relative_path);
    }
    Ok(game_path.join(normalized.replace('/', r"\")))
}

fn delete_empty_directories(game_path: &Path, files: &[String]) -> Result<()> {
    for relative_file in files {
        let target = safe_target_path(game_path, relative_file)?;
        let mut current = target.parent().map(Path::to_path_buf);
        while let Some(directory) = current {
            if directory == game_path || !directory.is_dir() {
                break;
            }
            if fs::read_dir(&directory)?.next().is_some() {
                break;
            }
            fs::remove_dir(&directory).with_context(|| format!("Failed to remove {}", directory.display()))?;
            current = directory.parent().map(Path::to_path_buf);
        }
    }
    Ok(())
}

fn normalize_relative_path(path: &str) -> String {
    path.replace('\\', "/").trim_start_matches('/').to_string()
}

fn format_version(lang: Lang, version: &str) -> String {
    if version.trim().is_empty() {
        tr(lang, Text::UnknownVersion).to_string()
    } else {
        version.to_string()
    }
}

fn wide(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(Some(0)).collect()
}

fn from_wide_z(value: &[u16]) -> String {
    let len = value.iter().position(|ch| *ch == 0).unwrap_or(value.len());
    String::from_utf16_lossy(&value[..len])
}

#[cfg(test)]
mod tests {
    use super::*;
    use zip::write::SimpleFileOptions;

    #[test]
    fn mod_asset_filter_accepts_release_zip_only() {
        assert!(is_mod_asset(&ReleaseAssetInfo {
            name: "AccessTheObelisk-v0.3.5.zip".to_string(),
            download_url: String::new(),
        }));
        assert!(!is_mod_asset(&ReleaseAssetInfo {
            name: "AccessTheObelisk.Installer.exe".to_string(),
            download_url: String::new(),
        }));
        assert!(!is_mod_asset(&ReleaseAssetInfo {
            name: "OtherMod-v0.3.5.zip".to_string(),
            download_url: String::new(),
        }));
    }

    #[test]
    fn russian_localization_filter_keeps_only_plugin_folder() {
        assert!(should_install_russian_localization_entry(
            r"BepInEx\plugins\RussianTranslation\RussianTranslation.dll"
        ));
        assert!(!should_install_russian_localization_entry(
            r"BepInEx\plugins\AccessTheObelisk\AccessTheObelisk.dll"
        ));
    }

    #[test]
    fn changelog_newlines_are_normalized_for_multiline_edit_control() {
        assert_eq!(
            windows_multiline_text("First\nSecond\r\nThird\rFourth"),
            "First\r\nSecond\r\nThird\r\nFourth"
        );
    }

    #[test]
    fn safe_target_path_rejects_path_traversal_and_absolute_paths() {
        let root = std::env::temp_dir().join("ato_installer_safe_path_test");
        let safe = safe_target_path(&root, "BepInEx/plugins/AccessTheObelisk/file.txt")
            .expect("safe path should be accepted");
        assert!(safe.ends_with(r"BepInEx\plugins\AccessTheObelisk\file.txt"));
        assert!(safe_target_path(&root, "../escape.txt").is_err());
        assert!(safe_target_path(&root, "C:/escape.txt").is_err());
        assert!(safe_target_path(&root, "BepInEx//bad.txt").is_err());
    }

    #[test]
    fn atom_fallback_builds_release_assets_without_github_api() {
        let atom = r#"
            <feed>
              <entry>
                <link rel="alternate" href="https://github.com/Tanduriel/Access-The-Obelisk/releases/tag/v0.3.5"/>
                <title>AccessTheObelisk 0.3.5</title>
                <content type="html">&lt;p&gt;Release notes&lt;/p&gt;</content>
              </entry>
            </feed>
        "#;
        let releases = parse_atom_releases(atom).expect("feed should parse");
        assert_eq!(releases.len(), 1);
        assert_eq!(releases[0].tag_name, "v0.3.5");
        assert_eq!(releases[0].assets[0].name, "AccessTheObelisk-v0.3.5.zip");
        assert!(releases[0].assets[0]
            .download_url
            .ends_with("/v0.3.5/AccessTheObelisk-v0.3.5.zip"));
        assert_eq!(releases[0].changelog, "Release notes");
    }

    #[test]
    fn shared_bepinex_files_are_not_owned_by_mod_uninstall() {
        assert!(is_shared_bepinex_file("BepInEx/core/BepInEx.dll"));
        assert!(is_shared_bepinex_file("BepInEx/config/BepInEx.cfg"));
        assert!(is_shared_bepinex_file(
            "doorstop_libs/libdoorstop_x64.dylib"
        ));
        assert!(is_shared_bepinex_file("run_bepinex.sh"));
        assert!(is_shared_bepinex_file("winhttp.dll"));
        assert!(!is_shared_bepinex_file(
            "BepInEx/plugins/AccessTheObelisk/AccessTheObelisk.dll"
        ));
        assert!(!is_shared_bepinex_file("prism.dll"));
    }

    #[test]
    fn russian_localization_zip_installs_and_uninstalls() {
        let root = std::env::temp_dir().join(format!(
            "ato_installer_russian_test_{}",
            std::process::id()
        ));
        let _ = fs::remove_dir_all(&root);
        fs::create_dir_all(&root).expect("test root should be created");
        let zip_path = root.join("russian.zip");

        {
            let file = File::create(&zip_path).expect("test zip should be created");
            let mut zip = zip::ZipWriter::new(file);
            zip.start_file(
                "BepInEx/plugins/RussianTranslation/RussianTranslation.dll",
                SimpleFileOptions::default(),
            )
            .expect("zip entry should be created");
            zip.write_all(b"test dll")
                .expect("zip entry should be written");
            zip.finish().expect("test zip should finish");
        }

        install_russian_localization(&root, &zip_path)
            .expect("Russian localization should install");
        let installed = root.join(
            r"BepInEx\plugins\RussianTranslation\RussianTranslation.dll",
        );
        assert!(installed.is_file());

        uninstall_package(&root, "RussianTranslation")
            .expect("Russian localization should uninstall");
        assert!(!installed.exists());
        let _ = fs::remove_dir_all(&root);
    }

    #[test]
    fn mod_uninstall_preserves_shared_bepinex_files() {
        let root = std::env::temp_dir().join(format!(
            "ato_installer_uninstall_test_{}",
            std::process::id()
        ));
        let _ = fs::remove_dir_all(&root);
        let core = root.join(r"BepInEx\core\BepInEx.dll");
        let plugin = root.join(
            r"BepInEx\plugins\AccessTheObelisk\AccessTheObelisk.dll",
        );
        fs::create_dir_all(core.parent().unwrap()).unwrap();
        fs::create_dir_all(plugin.parent().unwrap()).unwrap();
        fs::write(&core, b"core").unwrap();
        fs::write(&plugin, b"plugin").unwrap();
        write_installed_package(
            &root,
            &InstalledPackage {
                package_id: MOD_NAME.to_string(),
                version: "test".to_string(),
                files: vec![
                    "BepInEx/core/BepInEx.dll".to_string(),
                    "BepInEx/plugins/AccessTheObelisk/AccessTheObelisk.dll"
                        .to_string(),
                ],
            },
        )
        .unwrap();

        uninstall_package(&root, MOD_NAME).unwrap();
        assert!(core.is_file());
        assert!(!plugin.exists());
        let _ = fs::remove_dir_all(&root);
    }

    #[test]
    fn thunderstore_bepinex_pack_extracts_inner_folder_only() {
        let root = std::env::temp_dir().join(format!(
            "ato_installer_bepinex_pack_test_{}",
            std::process::id()
        ));
        let _ = fs::remove_dir_all(&root);
        fs::create_dir_all(&root).unwrap();
        let zip_path = root.join("bepinex-pack.zip");

        {
            let file = File::create(&zip_path).unwrap();
            let mut zip = zip::ZipWriter::new(file);
            let options = SimpleFileOptions::default();
            zip.start_file(
                "BepInExPack_AcrossTheObelisk/BepInEx/config/BepInEx.cfg",
                options,
            )
            .unwrap();
            zip.write_all(b"config").unwrap();
            zip.start_file(
                "BepInExPack_AcrossTheObelisk/winhttp.dll",
                options,
            )
            .unwrap();
            zip.write_all(b"doorstop").unwrap();
            zip.start_file("manifest.json", options).unwrap();
            zip.write_all(b"thunderstore metadata").unwrap();
            zip.finish().unwrap();
        }

        let game = root.join("game");
        fs::create_dir_all(&game).unwrap();
        extract_bepinex_pack_to_game(&game, &zip_path).unwrap();
        assert!(game.join(r"BepInEx\config\BepInEx.cfg").is_file());
        assert!(game.join("winhttp.dll").is_file());
        assert!(!game.join("manifest.json").exists());
        assert!(!game.join("BepInExPack_AcrossTheObelisk").exists());
        let _ = fs::remove_dir_all(&root);
    }

    #[test]
    #[ignore = "requires network access"]
    fn live_release_discovery_uses_api_or_public_feed() {
        let releases = get_releases().expect("release discovery should succeed");
        assert!(releases.iter().any(has_mod_asset));
    }
}
