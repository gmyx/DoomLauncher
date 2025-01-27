﻿using DoomLauncher.Controls;
using DoomLauncher.Forms;
using DoomLauncher.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DoomLauncher
{
    public partial class MainForm
    {
        private bool VerifyDatabase()
        {
            bool check = false;
            try
            {
                if (File.Exists(Path.Combine(LauncherPath.GetDataDirectory(), DbDataSourceAdapter.DatabaseFileName)))
                {
                    check = true;
                    // Still attempt to delete the init database here for people that manually update (not installed)
                    if (File.Exists(DbDataSourceAdapter.InitDatabaseFileName))
                        File.Delete(DbDataSourceAdapter.InitDatabaseFileName);
                    return check;
                }

                check = InitFileCheck(DbDataSourceAdapter.DatabaseFileName, DbDataSourceAdapter.InitDatabaseFileName, false);

                if (!check)
                {
                    MessageBox.Show(this, "Initialization failure. Could not find DoomLauncher database",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Util.DisplayUnexpectedException(this, ex);
            }

            return check;
        }

        private bool VerifyGameFilesDirectory()
        {
            if (Directory.Exists(AppConfiguration.GameFileDirectory.GetFullPath()))
                return true;

            bool check = false;

            try
            {
                InitGameFilesDebug();
                check = InitFileCheck("GameFiles", "GameFiles_", true);

                if (!check)
                {
                    MessageBox.Show(this, "Initialization failure. Could not find DoomLauncher GameFiles directory. Please update your settings to continue.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    DialogResult result;
                    bool success = false;

                    do
                    {
                        success = ShowSettings(true, out result);
                    } while (result != DialogResult.Cancel && !success);

                    check = success;
                }
            }
            catch (Exception ex)
            {
                Util.DisplayUnexpectedException(this, ex);
            }

            return check;
        }

        [Conditional("DEBUG")]
        private void InitGameFilesDebug()
        {
            try
            {
                string basePath = "GameFiles";

                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(Path.Combine(basePath, "Demos"));
                    Directory.CreateDirectory(Path.Combine(basePath, "SaveGames"));
                    Directory.CreateDirectory(Path.Combine(basePath, "Screenshots"));
                    Directory.CreateDirectory(Path.Combine(basePath, "Temp"));
                }
            }
            catch
            {
                // For local debug code only, just catch if we testing in debug outside of dev
            }
        }

        private bool InitFileCheck(string initFile, string file, bool directory)
        {
            string dataSource = Path.Combine(Directory.GetCurrentDirectory(), initFile);
            string initDataSource = Path.Combine(Directory.GetCurrentDirectory(), file);

            if (directory)
            {
                DirectoryInfo diInit = new DirectoryInfo(initDataSource);
                DirectoryInfo diSource = new DirectoryInfo(dataSource);

                if (diSource.Exists)
                {
                    if (diInit.Exists)
                        diInit.Delete(true);
                }
                else
                {
                    if (diInit.Exists)
                        diInit.MoveTo(dataSource);
                    else
                        return false;
                }
            }
            else
            {
                FileInfo fiInit = new FileInfo(initDataSource);

                if (File.Exists(dataSource))
                {
                    if (fiInit.Exists)
                        fiInit.Delete();
                }
                else
                {
                    if (fiInit.Exists)
                        fiInit.MoveTo(dataSource);
                    else
                        return false;
                }
            }

            return true;
        }

        private void BackupDatabase(string dataSource)
        {
            FileInfo fi = new FileInfo(dataSource);

            if (fi.Exists)
            {
                Directory.CreateDirectory(Path.Combine(LauncherPath.GetDataDirectory(), "Backup"));
                string backupName = GetBackupFileName(fi);

                FileInfo fiBackup = new FileInfo(backupName);
                if (!fiBackup.Exists)
                    fi.CopyTo(backupName);

                CleanupBackupDirectory();
            }
        }

        private void CleanupBackupDirectory()
        {
            string[] files = Directory.GetFiles(Path.Combine(LauncherPath.GetDataDirectory(), "Backup"), "*.sqlite");
            List<FileInfo> filesInfo = new List<FileInfo>();
            Array.ForEach(files, x => filesInfo.Add(new FileInfo(x)));
            List<FileInfo> filesInfoOrdered = filesInfo.OrderBy(x => x.CreationTime).ToList();

            while (filesInfoOrdered.Count > 10)
            {
                filesInfoOrdered.First().Delete();
                filesInfoOrdered.RemoveAt(0);
            }
        }

        private string GetBackupFileName(FileInfo fi)
        {
            string end = DateTime.Now.ToString("yyyy_MM_dd") + fi.Extension;
            return Path.Combine(fi.DirectoryName, "Backup", fi.Name.Replace(fi.Extension, end));
        }

        private void SetupTabBase(ITabView tabView, ColumnField[] columnTextFields, ColumnConfig[] colConfig, ContextMenuStrip menu, bool dragDrop)
        {
            tabView.SetColumnConfig(columnTextFields, colConfig);
            tabView.GameFileViewControl.SetContextMenuStrip(menu);
            tabView.GameFileViewControl.AllowDrop = dragDrop;
            tabView.DataSourceChanging += TabView_DataSourceChanging;
            SetGameFileViewEvents(tabView.GameFileViewControl, dragDrop);
        }

        private void SetupTabs()
        {
            SetShowTabHeaders();

            List<ITabView> tabViews = new List<ITabView>();
            ColumnConfig[] colConfig = DataCache.Instance.GetColumnConfig();
            GameFileViewFactory = new GameFileViewFactory(this, AppConfiguration.GameFileViewType);
            GameFileTileManager.Instance.Init(GameFileViewFactory);

            tabViews.Add(CreateTabViewRecent(colConfig));
            tabViews.Add(CreateTabViewLocal(colConfig));
            tabViews.Add(CreateTabViewUntagged(colConfig));
            tabViews.Add(CreateTabViewIwad(colConfig));
            tabViews.Add(CreateTabViewIdGames(colConfig));
            tabViews.AddRange(CreateTagTabs(GameFileViewFactory.DefaultColumnTextFields, colConfig));

            m_tabHandler = new TabHandler(tabControl);
            m_tabHandler.SetTabs(tabViews);
        }

        private void SetShowTabHeaders()
        {
            if (AppConfiguration.ShowTabHeaders)
            {
                tabControl.Appearance = TabAppearance.Normal;
                tabControl.SizeMode = TabSizeMode.Normal;
                tabControl.ItemSize = new Size(16, 20);
            }
            else
            {
                tabControl.Appearance = TabAppearance.FlatButtons;
                tabControl.SizeMode = TabSizeMode.Fixed;
                tabControl.ItemSize = new Size(0, 1);
            }
        }

        private IdGamesTabViewCtrl CreateTabViewIdGames(ColumnConfig[] colConfig)
        {
            ColumnField[] columnTextFields = new ColumnField[]
            {
                new ColumnField("Title", "Title"),
                new ColumnField("Author", "Author"),
                new ColumnField("ReleaseDate", "Release Date"),
                new ColumnField("Description", "Description"),
                new ColumnField("Rating", "Rating"),
            };

            IdGamesDataSourceAdapter = new IdGamesDataAdapater(AppConfiguration.IdGamesUrl, AppConfiguration.ApiPage, AppConfiguration.MirrorUrl);
            var factory = new GameFileViewFactory(this, GameFileViewType.GridView);
            IdGamesTabViewCtrl tabViewIdGames = new IdGamesTabViewCtrl(TabKeys.IdGamesKey, StaticTagData.GetFavoriteName(TabKeys.IdGamesKey), IdGamesDataSourceAdapter, DefaultGameFileSelectFields, factory);
            SetupTabBase(tabViewIdGames, columnTextFields, colConfig, mnuIdGames, false);
            return tabViewIdGames;
        }

        private IWadTabViewCtrl CreateTabViewIwad(ColumnConfig[] colConfig)
        {
            ColumnField[] columnTextFields = new ColumnField[]
            {
                new ColumnField("FileNameNoPath", "File"),
                new ColumnField("Title", "Title"),
                new ColumnField("LastPlayed", "Last Played")
            };

            IWadTabViewCtrl tabViewIwads = new IWadTabViewCtrl(TabKeys.IWadsKey, StaticTagData.GetFavoriteName(TabKeys.IWadsKey), DataSourceAdapter, DefaultGameFileSelectFields, DataCache.Instance.TagMapLookup, GameFileViewFactory);
            SetupTabBase(tabViewIwads, columnTextFields, colConfig, mnuLocal, true);
            return tabViewIwads;
        }

        private LocalTabViewCtrl CreateTabViewUntagged(ColumnConfig[] colConfig)
        {
            LocalTabViewCtrl tabViewUntagged = new UntaggedTabView(TabKeys.UntaggedKey, StaticTagData.GetFavoriteName(TabKeys.UntaggedKey), DataSourceAdapter, DefaultGameFileSelectFields, DataCache.Instance.TagMapLookup, GameFileViewFactory);
            SetupTabBase(tabViewUntagged, GameFileViewFactory.DefaultColumnTextFields, colConfig, mnuLocal, true);
            return tabViewUntagged;
        }

        private LocalTabViewCtrl CreateTabViewLocal(ColumnConfig[] colConfig)
        {
            LocalTabViewCtrl tabViewLocal = new LocalTabViewCtrl(TabKeys.LocalKey, StaticTagData.GetFavoriteName(TabKeys.LocalKey), DataSourceAdapter, DefaultGameFileSelectFields, DataCache.Instance.TagMapLookup, GameFileViewFactory);
            SetupTabBase(tabViewLocal, GameFileViewFactory.DefaultColumnTextFields, colConfig, mnuLocal, true);
            return tabViewLocal;
        }

        private OptionsTabViewCtrl CreateTabViewRecent(ColumnConfig[] colConfig)
        {
            OptionsTabViewCtrl tabViewRecent = new OptionsTabViewCtrl(TabKeys.RecentKey, StaticTagData.GetFavoriteName(TabKeys.RecentKey), DataSourceAdapter, DefaultGameFileSelectFields, DataCache.Instance.TagMapLookup, GameFileViewFactory);
            SetupTabBase(tabViewRecent, GameFileViewFactory.DefaultColumnTextFields, colConfig, mnuLocal, true);
            tabViewRecent.Options = new GameFileGetOptions();
            tabViewRecent.Options.Limit = 10;
            tabViewRecent.Options.OrderBy = OrderType.Desc;
            tabViewRecent.Options.OrderField = GameFileFieldType.Downloaded;
            return tabViewRecent;
        }

        private List<ITabView> CreateTagTabs(ColumnField[] columnTextFields, ColumnConfig[] colConfig)
        {
            List<ITabView> ret = new List<ITabView>();
            DataCache.Instance.UpdateTags();
            IEnumerable<ITagData> tags = DataCache.Instance.Tags.Where(x => x.HasTab);

            foreach (ITagData tag in tags)
                ret.Add(CreateTagTab(columnTextFields, colConfig, tag.FavoriteName, tag, false));

            return ret;
        }

        private TagTabView CreateTagTab(ColumnField[] columnTextFields, ColumnConfig[] colConfig, string name, ITagData tag, bool isNew)
        {
            //use the local tab configuration for new tabs
            if (isNew)
            {
                colConfig = colConfig.Where(x => x.Parent == "Local").ToArray();
                Array.ForEach(colConfig, x => x.Parent = tag.Name);
            }

            TagTabView tabView = new TagTabView(tag.TagID, name, DataSourceAdapter, DefaultGameFileSelectFields, tag, GameFileViewFactory);
            SetupTabBase(tabView, columnTextFields, colConfig, mnuLocal, true);

            if (tabView.GameFileViewControl is IGameFileColumnView columnView)
            {
                columnView.SetColumnFormat("ReleaseDate", CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern);
                columnView.SetColumnFormat("Downloaded", CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern);
                columnView.SetColumnFormat("LastPlayed", CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern);
            }

            return tabView;
        }

        private void RebuildUtilityToolStrip()
        {
            var utilities = DataSourceAdapter.GetUtilities();
            ToolStripMenuItem utilityToolStrip = mnuLocal.Items.Cast<ToolStripItem>().FirstOrDefault(x => x.Text == "Utility") as ToolStripMenuItem;

            while (utilityToolStrip.DropDownItems.Count > 2)
                utilityToolStrip.DropDownItems.RemoveAt(utilityToolStrip.DropDownItems.Count - 1);

            foreach (var utility in utilities)
                utilityToolStrip.DropDownItems.Add(utility.Name, null, utilityToolStripItem_Click);
        }

        private void RebuildTagToolStrip()
        {
            IGameFileView currentControl = GetCurrentViewControl();
            if (currentControl != null)
            {
                List<ITagData> addTags = new List<ITagData>();
                List<ITagData> removeTags = new List<ITagData>();

                DataCache.Instance.UpdateTags();

                foreach (var gameFile in SelectedItems(currentControl))
                {
                    if (gameFile.GameFileID.HasValue)
                    {
                        var gameFileTags = DataCache.Instance.TagMapLookup.GetTags(gameFile);
                        var currentRemoveTags = DataCache.Instance.Tags.Where(x => gameFileTags.Any(y => y.TagID == x.TagID));
                        var currentAddTags = DataCache.Instance.Tags.Except(currentRemoveTags);

                        addTags = addTags.Union(currentAddTags).ToList();
                        removeTags = removeTags.Union(currentRemoveTags).ToList();
                    }
                }

                ToolStripMenuItem tagToolStrip = mnuLocal.Items.Cast<ToolStripItem>().FirstOrDefault(x => x.Text == "Tag") as ToolStripMenuItem;
                ToolStripMenuItem removeTagToolStrip = mnuLocal.Items.Cast<ToolStripItem>().FirstOrDefault(x => x.Text == "Remove Tag") as ToolStripMenuItem;

                if (tagToolStrip != null)
                {
                    BuildTagToolStrip(tagToolStrip, addTags, tagToolStripItem_Click);
                    BuildTagToolStrip(removeTagToolStrip, removeTags, removeTagToolStripItem_Click);
                }
            }
        }

        private void BuildTagToolStrip(ToolStripMenuItem tagToolStrip, IEnumerable<ITagData> tags, EventHandler handler)
        {
            while (tagToolStrip.DropDownItems.Count > 2)
                tagToolStrip.DropDownItems.RemoveAt(tagToolStrip.DropDownItems.Count - 1);

            foreach (ITagData tag in tags)
            {
                var item = tagToolStrip.DropDownItems.Add(tag.FavoriteName, null, handler);
                item.Tag = tag.TagID;
            }
        }

        private void SetGameFileViewEvents(IGameFileView ctrl, bool dragDrop)
        {
            ctrl.ItemDoubleClick += ctrlView_RowDoubleClicked;
            ctrl.SelectionChange += ctrlView_SelectionChange;
            ctrl.ViewKeyPress += ctrlView_GridKeyPress;

            if (dragDrop)
            {
                ctrl.DragDrop += ctrlView_DragDrop;
                ctrl.DragEnter += ctrlView_DragEnter;
                ctrl.ViewKeyDown += ctrlView_GridKeyDown;
            }
        }

        private void ctrlView_GridKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
                HandleDelete();
        }

        private ProgressBarForm m_progressBarUpdate;

        private async Task Initialize()
        {
            string dataSource = Path.Combine(LauncherPath.GetDataDirectory(), DbDataSourceAdapter.DatabaseFileName);
            DataAccess access = new DataAccess(new SqliteDatabaseAdapter(), DbDataSourceAdapter.CreateConnectionString(dataSource));

            m_versionHandler = new VersionHandler(access, DbDataSourceAdapter.CreateAdapter(true), AppConfiguration);

            if (m_versionHandler.UpdateRequired())
            {
                m_versionHandler.UpdateProgress += handler_UpdateProgress;

                m_progressBarUpdate = CreateProgressBar("Updating...", ProgressBarStyle.Continuous);
                ProgressBarStart(m_progressBarUpdate);

                await Task.Run(() => ExecuteVersionUpdate());

                ProgressBarEnd(m_progressBarUpdate);

                AppConfiguration.Refresh(); //We have to refresh here because a column may have been added to the Configuration table
            }

            if (AppConfiguration.CleanTemp)
                CleanTempDirectory();

            DirectoryDataSourceAdapter = new DirectoryDataSourceAdapter(AppConfiguration.GameFileDirectory);
            DataCache.Instance.Init(DataSourceAdapter);
            DataCache.Instance.AppConfiguration.GameFileViewTypeChanged += AppConfiguration_GameFileViewTypeChanged;
            DataCache.Instance.TagMapLookup.TagMappingChanged += TagMapLookup_TagMappingChanged;
            DataCache.Instance.TagsChanged += DataCache_TagsChanged;

            CleanUpFiles();

            SetupTabs();
            RebuildUtilityToolStrip();
            BuildUtilityToolStrip();

            InitTagSelectControl();
            InitDownloadView();

            ctrlAssociationView.Initialize(DataSourceAdapter, AppConfiguration);
            ctrlAssociationView.FileDeleted += ctrlAssociationView_FileDeleted;
            ctrlAssociationView.FileOrderChanged += ctrlAssociationView_FileOrderChanged;
            ctrlAssociationView.RequestScreenshots += CtrlAssociationView_RequestScreenshots;
        }

        private void CleanUpFiles()
        {
            var cleanupFiles = DataSourceAdapter.GetCleanupFiles();
            foreach (var cleanupFile in cleanupFiles)
            {
                try
                {
                    File.Delete(cleanupFile.FileName);
                    DataSourceAdapter.DeleteCleanupFile(cleanupFile);
                }
                catch
                {
                    // Try again next time...
                }
            }
        }

        private void InitDownloadView()
        {
            m_downloadView = new DownloadView();
            m_downloadView.UserPlay += DownloadView_UserPlay;
            m_downloadHandler = new DownloadHandler(AppConfiguration.TempDirectory, m_downloadView);
        }

        private void InitTagSelectControl()
        {
            m_tagSelectControl.BorderStyle = BorderStyle.FixedSingle;
            m_tagSelectControl.TagSelectionChanged += TagSelectCtrl_TagSelectionChanged;
            m_tagSelectControl.StaticSelectionChanged += TagSelectCtrl_StaticSelectionChanged;
            m_tagSelectControl.PinChanged += TagSelectControl_PinChanged;
            m_tagSelectControl.ManageTags += TagSelectControl_ManageTags;
            m_tagSelectControl.Init(new TagSelectOptions() { HasTabOnly = true, ShowStatic = true, AllowRowSelect = true, 
                ShowPin = true, ShowMenu = true });
            m_tagSelectControl.SetPinned(AppConfiguration.TagSelectPinned);

            if (AppConfiguration.TagSelectPinned)
                SetTagControlPinned();
        }

        private void TagSelectControl_ManageTags(object sender, EventArgs e)
        {
            HandleManageTags();
        }

        private void TagSelectControl_PinChanged(object sender, EventArgs e)
        {
            if (m_tagPopup != null)
            {
                m_tagPopup.Hide();
                // For some reason the Popup messes with Min/Max Size...
                m_tagSelectControl.MaximumSize = new Size(0, 0);
                m_tagSelectControl.MinimumSize = new Size(0, 0);
            }

            if (m_tagSelectControl.Pinned)
            {
                SetTagControlPinned();
            }
            else
            {
                splitTagSelect.Panel1Collapsed = true;
                splitTagSelect.Panel1.Controls.Remove(m_tagSelectControl);
                DisplayTags();
            }
        }

        private void SetTagControlPinned()
        {
            splitTagSelect.Panel1Collapsed = false;
            splitTagSelect.Panel1.Controls.Add(m_tagSelectControl);
            m_tagSelectControl.Dock = DockStyle.Fill;
        }

        private void TagMapLookup_TagMappingChanged(object sender, ITagData[] tags)
        {
            if (InvokeRequired)
                Invoke(new Action<ITagData[]>(HandleTagMappingChanged), new object[] { tags });
            else
                HandleTagMappingChanged(tags);
        }

        private void HandleTagMappingChanged(ITagData[] tags)
        {
            Array.ForEach(tags, x => UpdateTagTabData(x.TagID));
            if (tags.Any(x => x.ExcludeFromOtherTabs))
                UpdateLocal();
        }

        private void DataCache_TagsChanged(object sender, EventArgs e)
        {
            if (m_tabHandler == null)
                return;

            foreach (var tabView in m_tabHandler.TabViews)
            {
                if (tabView is TagTabView tagTabView)
                {
                    ITagData findTag = DataCache.Instance.Tags.FirstOrDefault(x => x.TagID == tagTabView.TagDataSource.TagID);
                    if (findTag != null)
                        tagTabView.TagDataSource = findTag;
                }
            }
        }

        private void TagSelectCtrl_StaticSelectionChanged(object sender, string name)
        {
            m_tabHandler.SelectTabFromKey(name);
        }

        private void TagSelectCtrl_TagSelectionChanged(object sender, ITagData tag)
        {
            ITabView tagView = m_tabHandler.TabViewForTag(tag);
            if (tagView != null)
                m_tabHandler.SelectTabView(tagView);
        }

        private void AppConfiguration_GameFileViewTypeChanged(object sender, EventArgs e)
        {
            if (GameFileViewFactory.IsBaseViewTypeChange(GameFileViewFactory.DefaultType, AppConfiguration.GameFileViewType))
            {
                // Write any settings the user may have changed before the application is killed
                HandleFormClosing();
                Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Util.GetExecutableNoPath()));
                return;
            }

            GameFileViewFactory.UpdateDefaultType(AppConfiguration.GameFileViewType);
            GameFileTileManager.Instance.Init(GameFileViewFactory);
        }

        private void BuildUtilityToolStrip()
        {
            ToolStripMenuItem sortToolStrip = GetSortByToolStrip();

            foreach (var col in GameFileViewFactory.DefaultColumnTextFields)
                sortToolStrip.DropDownItems.Add(col.Title, null, sortToolStripItem_Click);
        }

        private ToolStripMenuItem GetSortByToolStrip()
        {
            return mnuLocal.Items.Cast<ToolStripItem>().FirstOrDefault(x => x.Text == "Sort By") as ToolStripMenuItem;
        }

        private async Task CheckForAppUpdate()
        {
            try
            {
                ApplicationUpdate applicationUpdate = new ApplicationUpdate(TimeSpan.FromSeconds(30));
                ApplicationUpdateInfo info = await applicationUpdate.GetUpdateApplicationInfo(Assembly.GetExecutingAssembly().GetName().Version);

                if (info != null)
                    SetUpdateAvailable(info);
                else
                    ApplicationUpdater.CleanupUpdateFiles(AppDomain.CurrentDomain.BaseDirectory);
            }
            catch
            {
                // no internet connection or bad connection, try again next time
            }
        }

        private void SetUpdateAvailable(ApplicationUpdateInfo info)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<ApplicationUpdateInfo>(SetUpdateAvailable), new object[] { info });
            }
            else
            {
                btnUpdate.Visible = true;
                btnUpdate.GlowOnce();
                m_updateControl.Initialize(AppConfiguration, info);
            }
        }

        private async Task CheckFirstInit()
        {
            if (!DataSourceAdapter.GetSourcePorts().Any()) //If no source ports setup then it's the first time setup, display welcome/setup info
            {
                InvokeHideSplashScreen();
                DisplayWelcome();
                HandleEditSourcePorts(true);
            }

            if (!DataSourceAdapter.GetIWads().Any()) //If no iwads then prompt to add iwads
            {
                InvokeHideSplashScreen();
                await HandleAddIWads();
                Invoke((MethodInvoker)delegate { tabControl.SelectedIndex = 3; }); //the user has only added iwads on setup, so set the tab to iwads on first launch so there is something to see
                DisplayInitSettings(); //give user the change set default port, iwad, skill
            }
            else
            {
                Invoke((MethodInvoker)delegate { tabControl.SelectedIndex = AppConfiguration.LastSelectedTabIndex; });
            }
        }

        private void DisplayInitSettings()
        {
            SettingsForm settings = new SettingsForm(DataSourceAdapter, AppConfiguration);
            settings.SetToLaunchSettingsTab();
            settings.StartPosition = FormStartPosition.CenterParent;
            settings.ShowDialog();

            AppConfiguration.Refresh();
        }

        private void DisplayWelcome()
        {
            Welcome welcome = new Welcome();
            welcome.StartPosition = FormStartPosition.CenterParent;
            welcome.ShowDialog();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            if (m_launchFile != null)
            {
                string addFile = m_launchFile;

                IGameFile launchFile = DataSourceAdapter.GetGameFile(m_launchFile);
                m_launchFile = null;

                if (launchFile == null && File.Exists(addFile))
                    HandleAddGameFiles(AddFileType.GameFile, new string[] { addFile });
                else
                    HandlePlay(new IGameFile[] { launchFile });
            }
        }

        private static bool ValidatePosition(AppConfiguration config)
        {
            if (config.WindowState == FormWindowState.Maximized)
            {
                //Maximized goes outside the bounds to hide the border, bring the rectangle in a more than safe amount to check if the monitor is still there
                //Windows 7 can be -4, later version are -6, could change based on DPI
                int offs = 32;
                Rectangle formRectangle = new Rectangle(config.AppX + offs, config.AppY + offs, config.AppWidth - offs * 2, config.AppHeight - offs * 2);
                return Screen.AllScreens.Any(x => x.WorkingArea.Contains(formRectangle));
            }
            else if (config.WindowState != FormWindowState.Minimized)
            {
                Point formPt = new Point(config.AppX, config.AppY);
                return Screen.AllScreens.Any(x => x.WorkingArea.Contains(formPt));
            }

            return true;
        }

        private void ExecuteVersionUpdate()
        {
            m_versionHandler.HandleVersionUpdate();
        }

        void handler_UpdateProgress(object sender, EventArgs e)
        {
            if (InvokeRequired)
                Invoke(new Action(UpdateVersionProgress));
            else
                UpdateVersionProgress();
        }

        void UpdateVersionProgress()
        {
            m_progressBarUpdate.Value = m_versionHandler.ProgressPercent;
        }

        private void SetupSearchFilters()
        {
            chkAutoSearch.Checked = (bool)AppConfiguration.GetTypedConfigValue(ConfigType.AutoSearch, typeof(bool));

            ctrlSearch.SearchTextChanged += ctrlSearch_SearchTextChanged;
            Util.SetDefaultSearchFields(ctrlSearch);
        }

        private void CreateSendToLink()
        {
            //http://stackoverflow.com/questions/234231/creating-application-shortcut-in-a-directory
            Type t = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")); //Windows Script Host Shell Object
            dynamic shell = Activator.CreateInstance(t);
            try
            {
                string sendToPath = Environment.ExpandEnvironmentVariables(@"%AppData%\Microsoft\Windows\SendTo");
                var lnk = shell.CreateShortcut(Path.Combine(sendToPath, "DoomLauncher.lnk"));

                // This should always exist, but a user did report not having this folder on Windows 10...
                if (!Directory.Exists(sendToPath))
                    Directory.CreateDirectory(sendToPath);

                try
                {
                    lnk.TargetPath = Path.Combine(Directory.GetCurrentDirectory(), Util.GetExecutableNoPath());
                    lnk.IconLocation = string.Format(Path.Combine(Directory.GetCurrentDirectory(), "DoomLauncher.ico"));
                    lnk.Save();
                }
                catch
                {
                    // Do not crash just for failing to create SendTo link
                }
                finally
                {
                    Marshal.FinalReleaseComObject(lnk);
                }
            }
            catch
            {
                // Do not crash just for failing to create SendTo link
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        void ctrlSearch_SearchTextChanged(object sender, EventArgs e)
        {
            if (chkAutoSearch.Checked && GetCurrentTabView() != null &&
                GetCurrentTabView().GetType() != typeof(IdGamesTabViewCtrl))
            {
                HandleSearch();
            }
        }

        private GameFileFieldType[] DefaultGameFileSelectFields
        {
            get
            {
                return new GameFileFieldType[]
                {
                    GameFileFieldType.GameFileID,
                    GameFileFieldType.Filename,
                    GameFileFieldType.Author,
                    GameFileFieldType.Title,
                    GameFileFieldType.Description,
                    GameFileFieldType.Downloaded,
                    GameFileFieldType.LastPlayed,
                    GameFileFieldType.ReleaseDate,
                    GameFileFieldType.Comments,
                    GameFileFieldType.Rating,
                    GameFileFieldType.MapCount,
                    GameFileFieldType.MinutesPlayed,
                    GameFileFieldType.IWadID
                };
            }
        }

        private GameFileViewFactory GameFileViewFactory { get; set; }
    }
}
