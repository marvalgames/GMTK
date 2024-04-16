using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AssetInventory
{
    public partial class IndexUI
    {
        private static readonly ProfilerMarker ProfileMarkerBulk = new ProfilerMarker("Bulk Download State");

        private int _deprecatedAssetsCount;
        private int _excludedAssetsCount;
        private int _registryPackageCount;
        private int _customPackageCount;
        private int _selectedMedia;
        private string _assetSearchPhrase;
        private Vector2 _assetsScrollPos;
        private Vector2 _bulkScrollPos;
        private Vector2 _imageScrollPos;
        private Rect _mediaRect;
        private float _nextAssetSearchTime;

        private SearchField AssetSearchField => _assetSearchField = _assetSearchField ?? new SearchField();
        private SearchField _assetSearchField;

        [SerializeField] private MultiColumnHeaderState assetMchState;
        private Rect AssetTreeRect => new Rect(20, 0, position.width - 40, position.height - 60);
        private TreeViewWithTreeModel<AssetInfo> AssetTreeView
        {
            get
            {
                if (_assetTreeViewState == null) _assetTreeViewState = new TreeViewState();

                MultiColumnHeaderState headerState = AssetTreeViewControl.CreateDefaultMultiColumnHeaderState(AssetTreeRect.width);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(assetMchState, headerState)) MultiColumnHeaderState.OverwriteSerializedFields(assetMchState, headerState);
                assetMchState = headerState;

                if (_assetTreeView == null)
                {
                    MultiColumnHeader mch = new MultiColumnHeader(headerState);
                    mch.canSort = false;
                    mch.height = MultiColumnHeader.DefaultGUI.minimumHeight;
                    mch.ResizeToFit();

                    _assetTreeView = new AssetTreeViewControl(_assetTreeViewState, mch, AssetTreeModel);
                    _assetTreeView.OnSelectionChanged += OnAssetTreeSelectionChanged;
                    _assetTreeView.OnDoubleClickedItem += OnAssetTreeDoubleClicked;
                    _assetTreeView.Reload();
                }
                return _assetTreeView;
            }
        }

        private TreeViewWithTreeModel<AssetInfo> _assetTreeView;
        private TreeViewState _assetTreeViewState;

        private TreeModel<AssetInfo> AssetTreeModel
        {
            get
            {
                if (_assetTreeModel == null) _assetTreeModel = new TreeModel<AssetInfo>(new List<AssetInfo> {new AssetInfo().WithTreeData("Root", depth: -1)});
                return _assetTreeModel;
            }
        }
        private TreeModel<AssetInfo> _assetTreeModel;

        private AssetInfo _selectedTreeAsset;
        private List<AssetInfo> _selectedTreeAssets;

        private long _assetTreeSelectionSize;
        private float _assetTreeSelectionTotalCosts;
        private float _assetTreeSelectionStoreCosts;
        private readonly Dictionary<string, Tuple<int, Color>> _assetBulkTags = new Dictionary<string, Tuple<int, Color>>();
        private int _packageDetailsTab;

        private void OnPackageListUpdated()
        {
            if (_assets == null) return;

            _requireLookupUpdate = true;
            _requireAssetTreeRebuild = true;

            Dictionary<string, PackageInfo> packages = AssetStore.GetPackages();
            foreach (KeyValuePair<string, PackageInfo> package in packages)
            {
                AssetInfo info = _assets.FirstOrDefault(a => a.AssetSource == Asset.Source.RegistryPackage && a.SafeName == package.Key);
                if (info == null)
                {
                    // new package found, persist
                    PackageImporter.Persist(package.Value);
                    continue;
                }

                info.Refresh();
                if (package.Value.versions.latestCompatible != info.LatestVersion && !package.Value.versions.latestCompatible.ToLowerInvariant().Contains("pre"))
                {
                    AssetInventory.SetPackageVersion(info, package.Value);
                }
            }
        }

        private void OnTagsChanged()
        {
            _requireLookupUpdate = true;
            _requireAssetTreeRebuild = true;
        }

        private void DrawPackageDownload(AssetInfo info, bool updateMode = false)
        {
            if (!string.IsNullOrEmpty(info.OriginalLocation))
            {
                if (!updateMode)
                {
                    if (string.IsNullOrWhiteSpace(info.DownloadedActual))
                    {
                        if (info.PackageSize > 0)
                        {
                            EditorGUILayout.HelpBox($"Not cached currently. Download the asset to access its content ({EditorUtility.FormatBytes(info.PackageSize)}).", MessageType.Warning);
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("Not cached currently. Download the asset to access its content.", MessageType.Warning);
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox($"Cache currently contains version {info.DownloadedActual} of a different listing for this package. Download this package to override it.", MessageType.Warning);
                    }
                }

                if (info.PackageDownloader != null)
                {
                    AssetDownloadState state = info.PackageDownloader.GetState();
                    switch (state.state)
                    {
                        case AssetDownloader.State.Downloading:
                            UIStyles.DrawProgressBar(state.progress, $"{EditorUtility.FormatBytes(state.bytesDownloaded)}");
                            break;

                        case AssetDownloader.State.Unavailable:
                            if (info.PackageDownloader.IsDownloadSupported() && GUILayout.Button("Download")) info.PackageDownloader.Download();
                            break;

                        case AssetDownloader.State.UpdateAvailable:
                            if (info.PackageDownloader.IsDownloadSupported() && GUILayout.Button("Download Update"))
                            {
                                info.WasOutdated = true;
                                info.PackageDownloader.Download();
                            }
                            break;
                    }
                }
            }
            else
            {
                if (!updateMode)
                {
                    if (info.IsLocationUnmappedRelative())
                    {
                        EditorGUILayout.HelpBox("The location of this package is stored relative and no mapping has been done yet in the settings for this system.", MessageType.Warning);
                    }
                    else if (info.AssetSource == Asset.Source.CustomPackage && !File.Exists(info.GetLocation(true)))
                    {
                        EditorGUILayout.HelpBox("The custom package has been deleted and is not available anymore.", MessageType.Warning);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("This package is new and metadata has not been collected yet. Update the index to have all metadata up to date.", MessageType.Warning);
                        if (GUILayout.Button(UIStyles.Content("Load Metadata"))) FetchAssetDetails(true, info.AssetId);
                    }
                }
                else if (info.AssetSource == Asset.Source.CustomPackage)
                {
                    EditorGUILayout.HelpBox("Automatic update not possible since package is not from the Asset Store.", MessageType.Info);
                }
            }
        }

        private void DrawPackageDetails(AssetInfo info, bool showMaintenance = false, bool showActions = true, bool startNewSection = true)
        {
            if (info.Id == 0) return;

            bool showExpanded = AssetInventory.Config.expandPackageDetails && _tab == 1;
            int labelWidth = 95;
            if (startNewSection)
            {
                GUILayout.BeginVertical("Package Details", "window", GUILayout.Width(GetInspectorWidth()), GUILayout.ExpandWidth(false));
                EditorGUILayout.Space();
            }
            else
            {
                EditorGUILayout.LabelField("Package", EditorStyles.largeLabel);
            }
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILabelWithText("Name", info.GetDisplayName(), labelWidth, info.Location, true);
            if (info.AssetSource == Asset.Source.RegistryPackage)
            {
                GUILabelWithText("Id", info.SafeName, labelWidth, info.SafeName, true);

                if (info.PackageSource == PackageSource.Local)
                {
                    GUILabelWithText("Version", info.Version, labelWidth);
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Version", EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    if (EditorGUILayout.DropdownButton(AssetStore.IsInstalled(info) ? UIStyles.Content(info.InstalledPackageVersion(), "Version to use") : UIStyles.Content("not installed, select version"), FocusType.Keyboard, GUILayout.ExpandWidth(false)))
                    {
                        VersionSelectionUI versionUI = new VersionSelectionUI();
                        versionUI.Init(info, newVersion =>
                        {
                            info.ForceTargetVersion(newVersion);

                            ImportUI importUI = ImportUI.ShowWindow();
                            importUI.Init(new List<AssetInfo> {info}, true);
                        });
                        PopupWindow.Show(_versionButtonRect, versionUI);
                    }
                    if (Event.current.type == EventType.Repaint) _versionButtonRect = GUILayoutUtility.GetLastRect();
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Updates", EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    EditorGUI.BeginChangeCheck();
                    info.UpdateStrategy = (Asset.Strategy)EditorGUILayout.EnumPopup(info.UpdateStrategy, GUILayout.ExpandWidth(false));
                    if (EditorGUI.EndChangeCheck())
                    {
                        AssetInventory.SetAssetUpdateStrategy(info, info.UpdateStrategy);
                        _requireAssetTreeRebuild = true;
                    }
                    GUILayout.EndHorizontal();
                }
            }
            if (!string.IsNullOrWhiteSpace(info.License)) GUILabelWithText("License", $"{info.License}");
            if (!string.IsNullOrWhiteSpace(info.GetDisplayPublisher()))
            {
                if (info.PublisherId > 0)
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Publisher", EditorStyles.boldLabel, GUILayout.Width(labelWidth - 2));
                    if (GUILayout.Button(UIStyles.Content(info.GetDisplayPublisher()), EditorStyles.linkLabel)) Application.OpenURL(info.GetPublisherLink());
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILabelWithText("Publisher", $"{info.GetDisplayPublisher()}");
                }
            }
            if (ShowAdvanced() && !string.IsNullOrWhiteSpace(info.GetDisplayCategory())) GUILabelWithText("Category", $"{info.GetDisplayCategory()}");
            if (ShowAdvanced() && info.PackageSize > 0) GUILabelWithText("Size", EditorUtility.FormatBytes(info.PackageSize));
            if (ShowAdvanced() && !string.IsNullOrWhiteSpace(info.SupportedUnityVersions)) GUILabelWithText("Unity", info.SupportedUnityVersions, 95, null, true);
            if (ShowAdvanced() && info.FirstRelease.Year > 1) GUILabelWithText("Released", info.FirstRelease.ToString("ddd, MMM d yyyy"));
            if (ShowAdvanced() && info.PurchaseDate.Year > 1) GUILabelWithText("Purchased", info.PurchaseDate.ToString("ddd, MMM d yyyy"));
            if (info.LastRelease.Year > 1)
            {
                GUILabelWithText("Last Update", info.LastRelease.ToString("ddd, MMM d yyyy") + (!string.IsNullOrEmpty(info.LatestVersion) ? $" ({info.LatestVersion})" : string.Empty));
            }
            else if (!string.IsNullOrEmpty(info.LatestVersion))
            {
                GUILabelWithText("Latest Version", info.LatestVersion);
            }
            if (ShowAdvanced())
            {
                string price = info.GetPriceText();
                GUILabelWithText("Price", price);
            }
            if (!string.IsNullOrWhiteSpace(info.AssetRating))
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(UIStyles.Content("Rating", "Rating given by Asset Store users"), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                if (int.TryParse(info.AssetRating, out int rating))
                {
                    if (rating <= 0)
                    {
                        EditorGUILayout.LabelField("Not enough ratings", GUILayout.MaxWidth(108));
                    }
                    else
                    {
                        Color oldCC = GUI.contentColor;
#if UNITY_2021_1_OR_NEWER
                        // favicon is not gold anymore                    
                        GUI.contentColor = new Color(0.992f, 0.694f, 0.004f);
#endif
                        for (int i = 0; i < rating; i++)
                        {
                            GUILayout.Button(EditorGUIUtility.IconContent("Favorite Icon"), EditorStyles.label, GUILayout.Width(EditorGUIUtility.singleLineHeight), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        }
                        GUI.contentColor = oldCC;
                        for (int i = rating; i < 5; i++)
                        {
                            GUILayout.Button(EditorGUIUtility.IconContent("Favorite"), EditorStyles.label, GUILayout.Width(EditorGUIUtility.singleLineHeight), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField($"{info.AssetRating} ");
                }
                EditorGUILayout.LabelField($"({info.RatingCount} ratings)", GUILayout.MaxWidth(80));
                GUILayout.EndHorizontal();
            }

            string packageTooltip = $"Internal Id: {info.AssetId}\nForeign Id: {info.ForeignId}\nUpload Id: {info.UploadId}\nCurrent State: {info.CurrentState}\nLocation: {info.GetLocation(true)}";
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(UIStyles.Content("Source", packageTooltip), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
            switch (info.AssetSource)
            {
                case Asset.Source.AssetStorePackage:
                    if (info.ForeignId > 0)
                    {
                        if (GUILayout.Button(UIStyles.Content("Asset Store"), EditorStyles.linkLabel)) Application.OpenURL(info.GetItemLink());
                    }
                    else
                    {
                        EditorGUILayout.LabelField(UIStyles.Content("Asset Store", packageTooltip), UIStyles.GetLabelMaxWidth());
                    }
                    break;

                case Asset.Source.RegistryPackage:
                    if (info.ForeignId > 0)
                    {
                        if (GUILayout.Button(UIStyles.Content("Asset Store"), EditorStyles.linkLabel)) Application.OpenURL(info.GetItemLink());
                    }
                    else
                    {
                        EditorGUILayout.LabelField(UIStyles.Content($"{info.AssetSource} ({info.PackageSource})", info.SafeName), UIStyles.GetLabelMaxWidth());
                    }
                    break;

                default:
                    EditorGUILayout.LabelField(UIStyles.Content(info.AssetSource.ToString(), packageTooltip), UIStyles.GetLabelMaxWidth());
                    break;
            }
            GUILayout.EndHorizontal();
            if (info.AssetSource != Asset.Source.AssetStorePackage && info.AssetSource != Asset.Source.RegistryPackage && info.ForeignId > 0)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Asset Link", EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                if (GUILayout.Button(UIStyles.Content("Asset Store"), EditorStyles.linkLabel)) Application.OpenURL(info.GetItemLink());
                GUILayout.EndHorizontal();
            }

            if (showMaintenance)
            {
                if (AssetInventory.Config.createBackups && info.AssetSource != Asset.Source.RegistryPackage)
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Backup", "Activate to create backups for this asset (done after every update cycle)."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    EditorGUI.BeginChangeCheck();
                    info.Backup = EditorGUILayout.Toggle(info.Backup);
                    if (EditorGUI.EndChangeCheck()) AssetInventory.SetAssetBackup(info, info.Backup);
                    GUILayout.EndHorizontal();
                }

                if (ShowAdvanced())
                {
                    if (info.AssetSource == Asset.Source.CustomPackage || info.AssetSource == Asset.Source.Archive || info.AssetSource == Asset.Source.AssetStorePackage)
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(UIStyles.Content("Extract", "Will keep the package extracted in the cache to minimize access delays at the cost of more hard disk space."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                        EditorGUI.BeginChangeCheck();
                        info.KeepExtracted = EditorGUILayout.Toggle(info.KeepExtracted);
                        if (EditorGUI.EndChangeCheck()) AssetInventory.SetAssetExtraction(info, info.KeepExtracted);
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Exclude", "Will not index the asset and not show existing index results in the search."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    EditorGUI.BeginChangeCheck();
                    info.Exclude = EditorGUILayout.Toggle(info.Exclude);
                    if (EditorGUI.EndChangeCheck())
                    {
                        AssetInventory.SetAssetExclusion(info, info.Exclude);
                        _requireLookupUpdate = true;
                        _requireSearchUpdate = true;
                        _requireAssetTreeRebuild = true;
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();
            if (showExpanded && info.PreviewTexture != null)
            {
                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Box(info.PreviewTexture, EditorStyles.centeredGreyMiniLabel, GUILayout.MaxWidth(GetInspectorWidth()), GUILayout.MaxHeight(100));
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            if (info.SafeName == Asset.NONE) EditorGUILayout.HelpBox("This is an automatically created package for managing indexed media files that are not associated with any other package.", MessageType.Info);
            if (info.IsDeprecated) EditorGUILayout.HelpBox("This asset is deprecated.", MessageType.Warning);
            if (info.IsAbandoned) EditorGUILayout.HelpBox("This asset is no longer available for download.", MessageType.Error);

            if (showActions)
            {
                if (info.CurrentSubState == Asset.SubState.Outdated) EditorGUILayout.HelpBox("This asset is outdated in the cache. It is recommended to delete it from the database and the file system.", MessageType.Info);
                if (info.AssetSource == Asset.Source.AssetStorePackage
                    || info.AssetSource == Asset.Source.CustomPackage
                    || info.AssetSource == Asset.Source.RegistryPackage
                    || info.AssetSource == Asset.Source.Archive
                    || (info.AssetSource == Asset.Source.Directory && info.SafeName != Asset.NONE))
                {
                    EditorGUILayout.Space();
                    if (info.AssetSource == Asset.Source.RegistryPackage)
                    {
                        if (info.IsIndirectPackageDependency())
                        {
                            EditorGUILayout.HelpBox("This package is an indirect dependency and changing the version will decouple it from the dependency lifecycle which can potentially lead to issues.", MessageType.Info);
                            EditorGUILayout.Space();
                        }
                        if (info.InstalledPackageVersion() != null)
                        {
                            if (info.TargetPackageVersion() != null)
                            {
                                if (info.InstalledPackageVersion() != info.TargetPackageVersion())
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    if (GUILayout.Button(UIStyles.Content($"Update to {info.TargetPackageVersion()}", "Update package to the version calculated from the selected update strategy.")))
                                    {
                                        ImportUI importUI = ImportUI.ShowWindow();
                                        importUI.Init(new List<AssetInfo> {info}, true);
                                    }
                                    string changeLogURL = info.GetChangeLogURL(info.TargetPackageVersion());
                                    if (!string.IsNullOrWhiteSpace(changeLogURL) && GUILayout.Button(UIStyles.Content("?", "Changelog"), GUILayout.Width(20)))
                                    {
                                        Application.OpenURL(changeLogURL);
                                    }
                                    EditorGUILayout.EndHorizontal();
                                }
                            }
                            if (GUILayout.Button(UIStyles.Content("Uninstall Package", "Remove package from current project.")))
                            {
                                Client.Remove(info.SafeName);
                            }
                            EditorGUILayout.Space();
                        }
                        else if (info.TargetPackageVersion() != null)
                        {
                            if (GUILayout.Button(UIStyles.Content($"Install Version {info.TargetPackageVersion()}", "Installs package into the current project.")))
                            {
                                ImportUI importUI = ImportUI.ShowWindow();
                                importUI.Init(new List<AssetInfo> {info}, true);
                            }
                        }
                    }
                    else if (info.Downloaded)
                    {
                        if (info.AssetSource != Asset.Source.Archive && info.AssetSource != Asset.Source.Directory)
                        {
                            if (showMaintenance && (info.IsUpdateAvailable(_assets) || info.WasOutdated))
                            {
                                DrawPackageDownload(info, true);
                            }
                            if (AssetStore.IsInstalled(info))
                            {
                                if (GUILayout.Button("Remove Package")) RemovePackage(info);
                            }
                            else
                            {
                                if (GUILayout.Button(UIStyles.Content("Import Package...", "Open import dialog")))
                                {
                                    ImportUI importUI = ImportUI.ShowWindow();
                                    importUI.Init(new List<AssetInfo> {info});
                                }
                                if (ShowAdvanced() && GUILayout.Button(UIStyles.Content("Open Package Location..."))) EditorUtility.RevealInFinder(info.GetLocation(true));
                            }
                        }
                        else
                        {
                            string locName = info.AssetSource == Asset.Source.Archive ? "Archive" : "Directory";
                            if (ShowAdvanced() && GUILayout.Button(UIStyles.Content($"Open {locName} Location..."))) EditorUtility.RevealInFinder(info.GetLocation(true));
                        }
                    }
                    if (ShowAdvanced() && (info.ForeignId > 0 || info.AssetSource == Asset.Source.RegistryPackage) && GUILayout.Button(UIStyles.Content("Open in Package Manager...")))
                    {
                        AssetStore.OpenInPackageManager(info);
                    }

                    if (_tab == 0 && _selectedAsset == 0 && GUILayout.Button("Filter for this package only")) OpenInSearch(info, true);
                    if (_tab != 1 && ShowAdvanced() && GUILayout.Button("Show in Package View")) OpenInPackageView(info);
                    if (_tab > 0 && info.IsIndexed && info.FileCount > 0)
                    {
                        if (GUILayout.Button("Open in Search")) OpenInSearch(info);
                        if (ShowAdvanced()) EditorGUILayout.Space();
                        if (ShowAdvanced() && info.Downloaded && GUILayout.Button(UIStyles.Content("Reindex Package on Next Run", "Will mark this package as outdated and force a reindex the next time Update Index is called on the Settings tab.")))
                        {
                            AssetInventory.ForgetAsset(info, true);
                            _requireLookupUpdate = true;
                            _requireSearchUpdate = true;
                            _requireAssetTreeRebuild = true;
                        }
                    }
                    if (showMaintenance)
                    {
                        if (ShowAdvanced() && info.Downloaded && GUILayout.Button(UIStyles.Content("Reindex Package Now", "Will instantly delete the existing index and reindex the full package.")))
                        {
                            AssetInventory.ForgetAsset(info, true);
                            AssetInventory.RefreshIndex(info.Id);
                            _requireLookupUpdate = true;
                            _requireSearchUpdate = true;
                            _requireAssetTreeRebuild = true;
                        }
                        if (ShowAdvanced() && info.ForeignId > 0 && GUILayout.Button(UIStyles.Content("Refresh Metadata", "Will fetch most up-to-date metadata from the Asset Store."))) FetchAssetDetails(true, info.AssetId);
                    }
                    if (info.Downloaded)
                    {
                        if (info.IsIndexed && info.FileCount > 0)
                        {
                            if (ShowAdvanced() && GUILayout.Button("Recreate Missing Previews")) RecreatePreviews(info.ToAsset(), true, false);
                            if (ShowAdvanced() && GUILayout.Button(UIStyles.Content("Recreate Image Previews", "Will iterate over all image files inside the package and redo them which is especially useful when changing the intended preview size on the Settings tab."))) RecreatePreviews(info.ToAsset(), false, true, AssetInventory.TypeGroups["Images"]);
                            if (ShowAdvanced() && GUILayout.Button(UIStyles.Content("Recreate All Previews", "Will mark all existing preview images to be redone and starts the process in the background."))) RecreatePreviews(info.ToAsset(), false, true);
                        }
                    }
                    else if (info.AssetSource == Asset.Source.CustomPackage || info.AssetSource == Asset.Source.Archive || info.AssetSource == Asset.Source.Directory)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.HelpBox("This package does not exist anymore on the file system and was probably deleted.", MessageType.Warning);
                    }
                    else if (!info.IsAbandoned)
                    {
                        EditorGUILayout.Space();
                        DrawPackageDownload(info);
                    }
                    if (showMaintenance && info.AssetSource == Asset.Source.CustomPackage)
                    {
                        if (info.ForeignId <= 0)
                        {
                            if (GUILayout.Button("Connect to Asset Store..."))
                            {
                                AssetConnectionUI assetUI = new AssetConnectionUI();
                                assetUI.Init(details => ConnectToAssetStore(info, details));
                                PopupWindow.Show(_connectButtonRect, assetUI);
                            }
                            if (Event.current.type == EventType.Repaint) _connectButtonRect = GUILayoutUtility.GetLastRect();
                        }
                        else
                        {
                            if (GUILayout.Button("Remove Asset Store Connection"))
                            {
                                bool removeMetadata = EditorUtility.DisplayDialog("Remove Metadata", "Remove or keep the additional metadata from the Asset Store like ratings, category etc.?", "Remove", "Keep");
                                AssetInventory.DisconnectFromAssetStore(info, removeMetadata);
                                _requireAssetTreeRebuild = true;
                            }
                            if (Event.current.type == EventType.Repaint) _connectButtonRect = GUILayoutUtility.GetLastRect();
                        }
                    }
                }
                if (showMaintenance)
                {
                    if (ShowAdvanced() && info.ForeignId > 0 && info.AssetSource != Asset.Source.RegistryPackage && GUILayout.Button("Export Package..."))
                    {
                        ExportUI exportUI = ExportUI.ShowWindow();
                        exportUI.Init(_selectedTreeAssets, 1);
                    }
                    if (ShowAdvanced()) EditorGUILayout.Space();
                    if (info.AssetSource != Asset.Source.RegistryPackage)
                    {
                        if (ShowAdvanced() && info.Downloaded && GUILayout.Button(UIStyles.Content("Delete Package...", "Delete the package from the database and optionally the file system.")))
                        {
                            bool removeFiles = info.Downloaded && EditorUtility.DisplayDialog("Delete Package", "Do you also want to remove the file from the Unity cache? If not the package will reappear after the next index update.", "Remove also from Cache", "Remove only from Database");
                            AssetInventory.RemoveAsset(info, removeFiles);
                            _requireLookupUpdate = true;
                            _requireAssetTreeRebuild = true;
                        }
                        if (ShowAdvanced() && !info.Downloaded && GUILayout.Button(UIStyles.Content("Delete Package", "Delete the package from the database.")))
                        {
                            AssetInventory.RemoveAsset(info, false);
                            _requireLookupUpdate = true;
                            _requireAssetTreeRebuild = true;
                        }
                        if (ShowAdvanced() && info.Downloaded && GUILayout.Button(UIStyles.Content("Delete Package from File System", "Delete the package only from the cache in the file system and leave index intact.")))
                        {
                            if (File.Exists(info.GetLocation(true)))
                            {
                                File.Delete(info.GetLocation(true));
                                info.Location = null;
                                info.PackageSize = 0;
                                info.CurrentState = Asset.State.New;
                                info.Refresh();
                                DBAdapter.DB.Execute("update Asset set Location=null, PackageSize=0, CurrentState=? where Id=?", info.AssetId, Asset.State.New);
                                _requireAssetTreeRebuild = true;
                            }
                        }
                    }
                    else
                    {
                        if (ShowAdvanced() && info.IsIndexed && GUILayout.Button(UIStyles.Content("Delete Package From Index", "Delete the package from the database.")))
                        {
                            AssetInventory.RemoveAsset(info, false);
                            _requireLookupUpdate = true;
                            _requireAssetTreeRebuild = true;
                        }
                    }
                }

                DrawAddPackageTag(new List<AssetInfo> {info});

                if (info.PackageTags != null && info.PackageTags.Count > 0)
                {
                    float x = 0f;
                    foreach (TagInfo tagInfo in info.PackageTags)
                    {
                        x = CalcTagSize(x, tagInfo.Name);
                        UIStyles.DrawTag(tagInfo, () =>
                        {
                            AssetInventory.RemoveTagAssignment(info, tagInfo);
                            _requireAssetTreeRebuild = true;
                        });
                    }
                }
                GUILayout.EndHorizontal();
            }
            if (showExpanded)
            {
                List<string> sections = new List<string>();
                if (info.Media != null && info.Media.Count > 0) sections.Add("Images");
                if (!string.IsNullOrWhiteSpace(info.Description)) sections.Add("Description");
                if (!string.IsNullOrWhiteSpace(info.ReleaseNotes)) sections.Add("Release Notes");
                if (info.AssetSource == Asset.Source.RegistryPackage) sections.Add("Dependencies");

                if (sections.Count > 0)
                {
                    EditorGUILayout.Space(20);
                    _packageDetailsTab = GUILayout.Toolbar(_packageDetailsTab, sections.ToArray(), GUILayout.Height(32), GUILayout.MinWidth(500));
                    if (_packageDetailsTab > sections.Count - 1) _packageDetailsTab = sections.Count - 1;
                    switch (sections[_packageDetailsTab])
                    {
                        case "Description":
                            if (!string.IsNullOrWhiteSpace(info.Description)) EditorGUILayout.LabelField(IOUtils.ToLabel(info.Description), EditorStyles.wordWrappedLabel);
                            break;

                        case "Release Notes":
                            if (!string.IsNullOrWhiteSpace(info.ReleaseNotes)) EditorGUILayout.LabelField(IOUtils.ToLabel(info.ReleaseNotes), EditorStyles.wordWrappedLabel);
                            break;

                        case "Images":
                            EditorGUILayout.Space();
                            GUILayout.BeginHorizontal();
                            GUILayout.FlexibleSpace();
                            GUILayout.Box(info.Media[_selectedMedia].Texture, UIStyles.centerLabel, GUILayout.MaxWidth(GetInspectorWidth() - 20), GUILayout.Height(AssetInventory.Config.mediaHeight));
                            if (Event.current.type == EventType.Repaint) _mediaRect = GUILayoutUtility.GetLastRect();
                            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                            {
                                if (_mediaRect.Contains(Event.current.mousePosition))
                                {
                                    // start process from thread as otherwise GUI reports layouting errors
                                    string path = info.ToAsset().GetMediaFile(info.Media[_selectedMedia], AssetInventory.GetPreviewFolder());
                                    Task _ = Task.Run(() => Process.Start(path));
                                }
                            }

                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();
                            _imageScrollPos = EditorGUILayout.BeginScrollView(_imageScrollPos, false, false, GUILayout.Height(AssetInventory.Config.mediaThumbnailHeight + 20));
                            GUILayout.BeginHorizontal();
                            for (int i = 0; i < info.Media.Count; i++)
                            {
                                AssetMedia media = info.Media[i];
                                Texture2D texture = media.ThumbnailTexture != null ? media.ThumbnailTexture : media.Texture;
                                if (GUILayout.Button(UIStyles.Content(texture == null ? "Loading..." : string.Empty, texture), GUILayout.Width(AssetInventory.Config.mediaThumbnailWidth), GUILayout.Height(AssetInventory.Config.mediaThumbnailHeight)))
                                {
                                    if (media.Type == "youtube")
                                    {
                                        // open URL in browser
                                        Application.OpenURL(media.GetUrl());
                                    }
                                    else
                                    {
                                        _selectedMedia = i;
                                    }
                                }
                            }
                            GUILayout.EndHorizontal();
                            EditorGUILayout.EndScrollView();
                            break;

                        case "Dependencies":
                            PackageInfo pInfo = AssetStore.GetPackageInfo(info);
                            if (!AssetStore.IsMetadataAvailable())
                            {
                                EditorGUILayout.HelpBox("Loading data...", MessageType.Info);
                            }
                            else if (pInfo == null || pInfo.dependencies == null)
                            {
                                EditorGUILayout.HelpBox("Could not find matching package metadata.", MessageType.Warning);
                            }
                            else
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.BeginVertical(GUILayout.Width(UIStyles.INSPECTOR_WIDTH - 30));
                                EditorGUILayout.LabelField("Is Using", EditorStyles.boldLabel);
                                if (pInfo.dependencies.Length > 0)
                                {
                                    foreach (DependencyInfo dependency in pInfo.dependencies.OrderBy(d => d.name))
                                    {
                                        AssetInfo package = _assets.FirstOrDefault(a => a.SafeName == dependency.name);
                                        if (package != null)
                                        {
                                            if (GUILayout.Button(package.GetDisplayName() + $" - {dependency.version}", GUILayout.ExpandWidth(false)))
                                            {
                                                OpenInPackageView(package);
                                            }
                                        }
                                        else
                                        {
                                            EditorGUILayout.LabelField($"{dependency.name} - {dependency.version}");
                                        }
                                    }
                                }
                                else
                                {
                                    EditorGUILayout.LabelField("-none-");
                                }
                                EditorGUILayout.EndVertical();

                                EditorGUILayout.Space();
                                EditorGUILayout.BeginVertical();
                                IEnumerable<PackageInfo> usedBy = AssetStore.GetPackages().Values.Where(p => p.dependencies.Select(d => d.name).Contains(info.SafeName));
                                EditorGUILayout.LabelField("Used By", EditorStyles.boldLabel);
                                if (usedBy.Any())
                                {
                                    foreach (PackageInfo dependency in usedBy.OrderBy(d => d.displayName))
                                    {
                                        AssetInfo package = _assets.FirstOrDefault(a => a.SafeName == dependency.name);
                                        if (package != null)
                                        {
                                            if (GUILayout.Button(package.GetDisplayName() + $" - {dependency.version}", GUILayout.ExpandWidth(false)))
                                            {
                                                OpenInPackageView(package);
                                            }
                                        }
                                        else
                                        {
                                            EditorGUILayout.LabelField($"{dependency.name} - {dependency.version}");
                                        }
                                    }
                                }
                                else
                                {
                                    EditorGUILayout.LabelField("-none-");
                                }
                                EditorGUILayout.EndVertical();
                                EditorGUILayout.EndHorizontal();
                            }
                            break;

                    }
                }
            }
            else
            {
                // highly condensed view
                if (info.PreviewTexture != null)
                {
                    EditorGUILayout.Space();
                    GUILayout.FlexibleSpace();
                    GUILayout.Box(info.PreviewTexture, EditorStyles.centeredGreyMiniLabel, GUILayout.MaxWidth(GetInspectorWidth()), GUILayout.MaxHeight(100));
                    GUILayout.FlexibleSpace();
                }
                else if (info.AssetSource == Asset.Source.RegistryPackage && !string.IsNullOrWhiteSpace(info.Description))
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(info.Description, EditorStyles.wordWrappedLabel);
                }
            }
            if (_tab == 1) RenderExpandButton();
            if (startNewSection) GUILayout.EndVertical();
        }

        private static void RemovePackage(AssetInfo info)
        {
            Client.Remove(info.SafeName);
        }

        private async void ConnectToAssetStore(AssetInfo info, AssetDetails details)
        {
            AssetInventory.ConnectToAssetStore(info, details);
            await AssetInventory.FetchAssetsDetails();
            _requireLookupUpdate = true;
            _requireAssetTreeRebuild = true;
        }

        private float CalcTagSize(float x, string tagName)
        {
            x += UIStyles.tag.CalcSize(UIStyles.Content(tagName)).x + UIStyles.TAG_SIZE_SPACING + EditorGUIUtility.singleLineHeight + UIStyles.tag.margin.right * 2f;
            if (x > GetInspectorWidth() - UIStyles.TAG_OUTER_MARGIN * 3)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(95 + 3);
                x = UIStyles.tag.CalcSize(UIStyles.Content(tagName)).x + UIStyles.TAG_SIZE_SPACING + EditorGUIUtility.singleLineHeight + UIStyles.tag.margin.right * 2f;
            }
            return x;
        }

        private void DrawAddPackageTag(List<AssetInfo> info)
        {
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(UIStyles.Content("Add Tag..."), GUILayout.Width(80)))
            {
                TagSelectionUI tagUI = new TagSelectionUI();
                tagUI.Init(TagAssignment.Target.Package);
                tagUI.SetAssets(info);
                PopupWindow.Show(_tagButtonRect, tagUI);
            }
            if (Event.current.type == EventType.Repaint) _tagButtonRect = GUILayoutUtility.GetLastRect();
            GUILayout.Space(15);
        }

        private void DrawPackagesTab()
        {
            // asset list
            if (_packageCount == 0)
            {
                EditorGUILayout.HelpBox("No packages were indexed yet. Start the indexing process to fill this list.", MessageType.Info);
                GUILayout.BeginHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(EditorGUIUtility.IconContent("Preset.Context", "|Search Filters")))
                {
                    AssetInventory.Config.showPackageFilterBar = !AssetInventory.Config.showPackageFilterBar;
                    AssetInventory.SaveConfig();
                }
                EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
                EditorGUI.BeginChangeCheck();
                _assetSearchPhrase = AssetSearchField.OnGUI(_assetSearchPhrase, GUILayout.Width(120));
                if (EditorGUI.EndChangeCheck())
                {
                    // delay search to allow fast typing
                    _nextAssetSearchTime = Time.realtimeSinceStartup + AssetInventory.Config.searchDelay;
                }
                else if (!_allowLogic && _nextAssetSearchTime > 0 && Time.realtimeSinceStartup > _nextAssetSearchTime) // don't do when logic allowed as otherwise there will be GUI errors
                {
                    _nextAssetSearchTime = 0;
                    _requireAssetTreeRebuild = true;
                }

                EditorGUI.BeginChangeCheck();
                if (AssetInventory.Config.assetGrouping == 0)
                {
                    EditorGUILayout.Space();
                    EditorGUIUtility.labelWidth = 50;
                    AssetInventory.Config.assetSorting = EditorGUILayout.Popup(UIStyles.Content("Sort by:", "Specify how packages should be sorted"), AssetInventory.Config.assetSorting, _assetSortOptions, GUILayout.Width(160));
                    if (GUILayout.Button(AssetInventory.Config.sortAssetsDescending ? UIStyles.Content("˅", "Descending") : UIStyles.Content("˄", "Ascending"), GUILayout.Width(15)))
                    {
                        AssetInventory.Config.sortAssetsDescending = !AssetInventory.Config.sortAssetsDescending;
                    }
                }

                EditorGUILayout.Space();
                EditorGUIUtility.labelWidth = 60;
                AssetInventory.Config.assetGrouping = EditorGUILayout.Popup(UIStyles.Content("Group by:", "Select if packages should be grouped or not"), AssetInventory.Config.assetGrouping, _groupByOptions, GUILayout.Width(140));
                if (EditorGUI.EndChangeCheck())
                {
                    CreateAssetTree();
                    AssetInventory.SaveConfig();
                }

                EditorGUIUtility.labelWidth = 0;

                if (AssetInventory.Config.assetGrouping > 0)
                {
                    if (GUILayout.Button("Expand All", GUILayout.ExpandWidth(false)))
                    {
                        AssetTreeView.ExpandAll();
                    }
                    if (GUILayout.Button("Collapse All", GUILayout.ExpandWidth(false)))
                    {
                        AssetTreeView.CollapseAll();
                    }
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Types:", GUILayout.Width(41));
                EditorGUI.BeginChangeCheck();
                AssetInventory.Config.packagesListing = GUILayout.Toolbar(AssetInventory.Config.packagesListing, _packageListingOptionsShort, GUILayout.Width(350));
                if (EditorGUI.EndChangeCheck())
                {
                    CreateAssetTree();
                    AssetInventory.SaveConfig();
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                EditorGUILayout.Space();
                EditorGUILayout.Space();
                GUILayout.BeginHorizontal();
                if (AssetInventory.Config.showPackageFilterBar)
                {
                    GUILayout.BeginVertical("Filter Bar", "window", GUILayout.Width(UIStyles.INSPECTOR_WIDTH));
                    EditorGUILayout.Space();

                    EditorGUI.BeginChangeCheck();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Packages", EditorStyles.boldLabel, GUILayout.Width(85));
                    AssetInventory.Config.packagesListing = EditorGUILayout.Popup(AssetInventory.Config.packagesListing, _packageListingOptions, GUILayout.ExpandWidth(true));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Deprecation", EditorStyles.boldLabel, GUILayout.Width(85));
                    AssetInventory.Config.assetDeprecation = EditorGUILayout.Popup(AssetInventory.Config.assetDeprecation, _deprecationOptions, GUILayout.ExpandWidth(true));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Maintenance", "A collection of various special-purpose filters"), EditorStyles.boldLabel, GUILayout.Width(85));
                    _selectedMaintenance = EditorGUILayout.Popup(_selectedMaintenance, _maintenanceOptions, GUILayout.ExpandWidth(true));
                    GUILayout.EndHorizontal();

                    if (EditorGUI.EndChangeCheck())
                    {
                        AssetInventory.SaveConfig();
                        _requireAssetTreeRebuild = true;
                    }

                    EditorGUILayout.Space();
                    if ((AssetInventory.Config.packagesListing > 0 || AssetInventory.Config.assetDeprecation > 0 || _selectedMaintenance > 0) && GUILayout.Button("Reset Filters"))
                    {
                        AssetInventory.Config.packagesListing = 0;
                        AssetInventory.Config.assetDeprecation = 0;
                        _selectedMaintenance = 0;
                        _requireAssetTreeRebuild = true;

                        AssetInventory.SaveConfig();
                    }

                    GUILayout.EndVertical();
                }

                // packages
                GUILayout.BeginVertical();
                int left = AssetInventory.Config.showPackageFilterBar ? UIStyles.INSPECTOR_WIDTH + 5 : 0;
                int yStart = string.IsNullOrEmpty(CloudProjectSettings.accessToken) ? 128 : 80;
                AssetTreeView.OnGUI(new Rect(left, yStart, position.width - GetInspectorWidth() - left - 5, position.height - yStart));
                GUILayout.EndVertical();
            }
            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            // FIXME: scrolling is broken for some reason, bar will often overlap
            _assetsScrollPos = GUILayout.BeginScrollView(_assetsScrollPos, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandWidth(true));
            if (!AssetInventory.Config.expandPackageDetails || _selectedTreeAsset == null)
            {
                GUILayout.BeginVertical("Overview", "window", GUILayout.Width(GetInspectorWidth()), GUILayout.ExpandHeight(false));
                EditorGUILayout.Space();
                int labelWidth = 130;
                GUILabelWithText("Indexed Packages", $"{_indexedPackageCount:N0}/{_assets.Count:N0}", labelWidth);
                if (_purchasedAssetsCount > 0) GUILabelWithText($"{UIStyles.INDENT}Asset Store", $"{_purchasedAssetsCount:N0}", labelWidth);
                if (_registryPackageCount > 0) GUILabelWithText($"{UIStyles.INDENT}Registries", $"{_registryPackageCount:N0}", labelWidth);
                if (_customPackageCount > 0) GUILabelWithText($"{UIStyles.INDENT}Other Sources", $"{_customPackageCount:N0}", labelWidth);
                if (_deprecatedAssetsCount > 0) GUILabelWithText($"{UIStyles.INDENT}Deprecated", $"{_deprecatedAssetsCount:N0}", labelWidth);
                if (_excludedAssetsCount > 0) GUILabelWithText($"{UIStyles.INDENT}Excluded", $"{_excludedAssetsCount:N0}", labelWidth);
                if (_packageFileCount > 0) GUILabelWithText("Indexed Files", $"{_packageFileCount:N0}", labelWidth);

                if (_selectedTreeAsset == null && (_selectedTreeAssets == null || _selectedTreeAssets.Count == 0)) RenderExpandButton();

                GUILayout.EndVertical();
            }

            if (_selectedTreeAsset != null)
            {
                if (!AssetInventory.Config.expandPackageDetails) EditorGUILayout.Space();
                DrawPackageDetails(_selectedTreeAsset, true);
            }
            else if (_selectedTreeAsset == null && _selectedTreeAssets != null && _selectedTreeAssets.Count > 0)
            {
                DrawBulkPackageActions(_selectedTreeAssets, _assetBulkTags, _assetTreeSelectionSize, _assetTreeSelectionTotalCosts, _assetTreeSelectionStoreCosts, true);
            }
            GUILayout.EndScrollView();
            if (!ShowAdvanced() && AssetInventory.Config.showHints) EditorGUILayout.LabelField("Hold down CTRL for additional options.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private static void RenderExpandButton()
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(EditorGUIUtility.IconContent("d_ScaleTool", "|Expand/Collapse Details Section"), GUILayout.Width(28), GUILayout.Height(28)))
            {
                AssetInventory.Config.expandPackageDetails = !AssetInventory.Config.expandPackageDetails;
                AssetInventory.SaveConfig();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawBulkPackageActions(List<AssetInfo> bulkAssets, Dictionary<string, Tuple<int, Color>> bulkTags, long size, float totalCosts, float storeCosts, bool useScroll)
        {
            int labelWidth = 130;

            EditorGUILayout.Space();
            GUILayout.BeginVertical("Bulk Info", "window", GUILayout.Width(GetInspectorWidth()), GUILayout.ExpandHeight(false));
            EditorGUILayout.Space();
            GUILabelWithText("Selected Items", $"{bulkAssets.Count:N0}", labelWidth);
            GUILabelWithText("Size on Disk", EditorUtility.FormatBytes(size), labelWidth);
            if (ShowAdvanced())
            {
                if (totalCosts > 0)
                {
                    EditorGUILayout.Space();
                    GUILabelWithText("Total Price", bulkAssets[0].GetPriceText(totalCosts), labelWidth);
                }
                if (storeCosts > 0 && totalCosts > storeCosts)
                {
                    GUILabelWithText($"{UIStyles.INDENT}Asset Store", bulkAssets[0].GetPriceText(storeCosts), labelWidth);
                    GUILabelWithText($"{UIStyles.INDENT}Other Sources", bulkAssets[0].GetPriceText(totalCosts - storeCosts), labelWidth);
                    EditorGUILayout.Space();
                }
            }
            GUILayout.EndVertical();

            labelWidth = 100;
            EditorGUILayout.Space();
            GUILayout.BeginVertical("Bulk Actions", "window", GUILayout.Width(GetInspectorWidth()));
            EditorGUILayout.Space();
            if (useScroll) _bulkScrollPos = GUILayout.BeginScrollView(_bulkScrollPos, false, false);
            UpdateObserver updateObserver = AssetInventory.GetObserver();
            if (!updateObserver.PrioInitializationDone)
            {
                int progress = Mathf.RoundToInt(updateObserver.PrioInitializationProgress * 100f);
                EditorGUILayout.HelpBox($"Gathering data (*): {progress}%", MessageType.Info);
                EditorGUILayout.Space();
            }
            if (AssetInventory.Config.createBackups)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Backup", EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                if (GUILayout.Button("All", GUILayout.ExpandWidth(false)))
                {
                    bulkAssets.ForEach(info => AssetInventory.SetAssetBackup(info, true));
                }
                if (GUILayout.Button("None", GUILayout.ExpandWidth(false)))
                {
                    bulkAssets.ForEach(info => AssetInventory.SetAssetBackup(info, false));
                }
                GUILayout.EndHorizontal();
            }

            if (ShowAdvanced())
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(UIStyles.Content("Extract", "Will keep the package extracted in the cache to minimize access delays at the cost of more hard disk space."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                if (GUILayout.Button("All", GUILayout.ExpandWidth(false)))
                {
                    bulkAssets.ForEach(info => AssetInventory.SetAssetExtraction(info, true));
                }
                if (GUILayout.Button("None", GUILayout.ExpandWidth(false)))
                {
                    bulkAssets.ForEach(info => AssetInventory.SetAssetExtraction(info, false));
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Exclude", EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                if (GUILayout.Button("All", GUILayout.ExpandWidth(false)))
                {
                    bulkAssets.ForEach(info => AssetInventory.SetAssetExclusion(info, true));
                    _requireLookupUpdate = true;
                    _requireSearchUpdate = true;
                    _requireAssetTreeRebuild = true;
                }
                if (GUILayout.Button("None", GUILayout.ExpandWidth(false)))
                {
                    bulkAssets.ForEach(info => AssetInventory.SetAssetExclusion(info, false));
                    _requireLookupUpdate = true;
                    _requireSearchUpdate = true;
                    _requireAssetTreeRebuild = true;
                }
                GUILayout.EndHorizontal();
            }

            // determine download status, a bit expensive but happens only in bulk selections
            ProfileMarkerBulk.Begin();
            int notDownloaded = 0;
            int updateAvailable = 0;
            int packageUpdateAvailable = 0;
            int updateAvailableButCustom = 0;
            int downloading = 0;
            long remainingBytes = 0;
            foreach (AssetInfo info in bulkAssets.Where(a => a.WasOutdated || !a.Downloaded || a.IsUpdateAvailable(_assets, false)))
            {
                if (info.AssetSource == Asset.Source.RegistryPackage)
                {
                    if (info.IsUpdateAvailable()) packageUpdateAvailable++;
                }
                else
                {
                    AssetDownloadState state = info.PackageDownloader.GetState();
                    switch (state.state)
                    {
                        case AssetDownloader.State.Unavailable:
                            notDownloaded++;
                            break;

                        case AssetDownloader.State.Downloading:
                            downloading++;
                            remainingBytes += state.bytesTotal - state.bytesDownloaded;
                            break;

                        case AssetDownloader.State.UpdateAvailable:
                            updateAvailable++;
                            break;

                        case AssetDownloader.State.Unknown:
                            if (info.AssetSource == Asset.Source.CustomPackage && info.IsUpdateAvailable(_assets))
                            {
                                updateAvailableButCustom++;
                            }
                            break;
                    }
                }
            }
            ProfileMarkerBulk.End();

            string initializing = updateObserver.PrioInitializationDone ? "" : "*";
            if (notDownloaded > 0)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Not Cached" + initializing, EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                if (GUILayout.Button($"Download remaining {notDownloaded}", GUILayout.ExpandWidth(false)))
                {
                    foreach (AssetInfo info in bulkAssets.Where(a => !a.Downloaded))
                    {
                        info.PackageDownloader.Download();
                    }
                }
                GUILayout.EndHorizontal();
            }
            if (updateAvailable > 0)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Cache Updates" + initializing, EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                if (GUILayout.Button("Download " + (downloading > 0 ? "remaining " : "") + updateAvailable, GUILayout.ExpandWidth(false)))
                {
                    foreach (AssetInfo info in bulkAssets.Where(a => a.IsUpdateAvailable(_assets) && a.PackageDownloader != null))
                    {
                        if (info.PackageDownloader.GetState().state == AssetDownloader.State.UpdateAvailable)
                        {
                            info.WasOutdated = true;
                            info.PackageDownloader.Download();
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }
            if (packageUpdateAvailable > 0)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Packages" + initializing, EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                if (GUILayout.Button($"Update {packageUpdateAvailable} registry packages", GUILayout.ExpandWidth(false)))
                {
                    List<AssetInfo> bulkList = bulkAssets
                        .Where(a => a.AssetSource == Asset.Source.RegistryPackage && a.IsUpdateAvailable())
                        .ToList();
                    ImportUI importUI = ImportUI.ShowWindow();
                    importUI.Init(bulkList, true);
                }
                GUILayout.EndHorizontal();
            }
            if (updateAvailableButCustom > 0)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox($"{updateAvailableButCustom}{initializing} updates cannot be performed since the assets are local custom packages and not from the Asset Store.", MessageType.Info);
                GUILayout.EndHorizontal();
            }

            if (downloading > 0)
            {
                GUILabelWithText("Downloading" + initializing, $"{downloading}", labelWidth);
                GUILabelWithText("Remaining" + initializing, $"{EditorUtility.FormatBytes(remainingBytes)}", labelWidth);
            }
            EditorGUILayout.Space();

            if (GUILayout.Button("Import..."))
            {
                ImportUI importUI = ImportUI.ShowWindow();
                importUI.Init(bulkAssets);
            }
            if (ShowAdvanced() && GUILayout.Button(UIStyles.Content("Open Package Locations...")))
            {
                bulkAssets.ForEach(info => { EditorUtility.RevealInFinder(info.GetLocation(true)); });
            }

            if (GUILayout.Button(UIStyles.Content("Reindex Packages on Next Run", "Will mark packages as outdated and force a reindex the next time Update Index is called on the Settings tab.")))
            {
                bulkAssets.ForEach(info => AssetInventory.ForgetAsset(info, true));
                _requireLookupUpdate = true;
                _requireSearchUpdate = true;
                _requireAssetTreeRebuild = true;
            }
            if (ShowAdvanced() && GUILayout.Button(UIStyles.Content("Refresh Metadata", "Will fetch most up-to-date metadata from the Asset Store.")))
            {
                bulkAssets.ForEach(info => FetchAssetDetails(true, info.AssetId));
            }
            if (ShowAdvanced() && GUILayout.Button("Export packages..."))
            {
                ExportUI exportUI = ExportUI.ShowWindow();
                exportUI.Init(bulkAssets, 1);
            }
            if (ShowAdvanced())
            {
                EditorGUILayout.Space();
                if (GUILayout.Button(UIStyles.Content("Delete Packages...", "Delete the packages from the database and optionally the file system.")))
                {
                    bool removeFiles = bulkAssets.Any(a => a.Downloaded) && EditorUtility.DisplayDialog("Delete Packages", "Do you also want to remove the files from the Unity cache? If not the packages will reappear after the next index update.", "Remove also from Cache", "Remove only from Database");
                    bulkAssets.ForEach(info => AssetInventory.RemoveAsset(info, removeFiles));
                    _requireLookupUpdate = true;
                    _requireAssetTreeRebuild = true;
                    _requireSearchUpdate = true;
                }
                if (GUILayout.Button(UIStyles.Content("Delete Packages from File System", "Delete the packages directly from the cache in the file system.")))
                {
                    bulkAssets.ForEach(info =>
                    {
                        if (File.Exists(info.GetLocation(true)))
                        {
                            File.Delete(info.GetLocation(true));
                            info.Refresh();
                        }
                    });
                    _requireSearchUpdate = true;
                }
            }

            DrawAddPackageTag(bulkAssets);

            float x = 0f;
            foreach (KeyValuePair<string, Tuple<int, Color>> bulkTag in bulkTags)
            {
                string tagName = $"{bulkTag.Key} ({bulkTag.Value.Item1})";
                x = CalcTagSize(x, tagName);
                UIStyles.DrawTag(tagName, bulkTag.Value.Item2, () =>
                {
                    AssetInventory.RemovePackageTagAssignment(bulkAssets, bulkTag.Key);
                    _requireAssetTreeRebuild = true;
                }, UIStyles.TagStyle.Remove);
            }
            GUILayout.EndHorizontal();

            RenderExpandButton();

            if (useScroll) GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void CreateAssetTree()
        {
            _requireAssetTreeRebuild = false;
            List<AssetInfo> data = new List<AssetInfo>();
            AssetInfo root = new AssetInfo().WithTreeData("Root", depth: -1);
            data.Add(root);

            // apply filters
            IEnumerable<AssetInfo> filteredAssets = _assets;
            switch (AssetInventory.Config.assetDeprecation)
            {
                case 1:
                    filteredAssets = filteredAssets.Where(a => !a.IsDeprecated && !a.IsAbandoned);
                    break;

                case 2:
                    filteredAssets = filteredAssets.Where(a => a.IsDeprecated || a.IsAbandoned);
                    break;
            }
            switch (_selectedMaintenance)
            {
                case 1:
                    filteredAssets = filteredAssets.Where(a => a.IsUpdateAvailable(_assets, false) || a.WasOutdated);
                    break;

                case 2:
                    filteredAssets = filteredAssets.Where(a => a.CurrentSubState == Asset.SubState.Outdated);
                    break;

                case 3:
                    filteredAssets = filteredAssets.Where(a => (a.AssetSource == Asset.Source.AssetStorePackage || (a.AssetSource == Asset.Source.RegistryPackage && a.ForeignId > 0)) && a.OfficialState == "disabled");
                    break;

                case 4:
                    filteredAssets = filteredAssets.Where(a => a.AssetSource == Asset.Source.CustomPackage && a.ForeignId > 0);
                    break;

                case 5:
                    filteredAssets = filteredAssets.Where(a => a.FileCount > 0);
                    break;

                case 6:
                    filteredAssets = filteredAssets.Where(a => a.FileCount == 0);
                    break;

                case 7:
                    filteredAssets = filteredAssets.Where(a => !string.IsNullOrEmpty(a.Registry) && a.Registry != "Unity");
                    break;

                case 8:
                    filteredAssets = filteredAssets.Where(AssetStore.IsInstalled);
                    break;

                case 9:
                    filteredAssets = filteredAssets.Where(a => a.AssetSource == Asset.Source.AssetStorePackage && a.Downloaded);
                    break;

                case 10:
                    filteredAssets = filteredAssets.Where(a => a.IsDownloading());
                    break;

                case 11:
                    filteredAssets = filteredAssets.Where(a => a.AssetSource == Asset.Source.AssetStorePackage && !a.Downloaded);
                    break;

                case 12:
                    List<int> duplicates = filteredAssets.Where(a => a.ForeignId > 0).GroupBy(a => a.ForeignId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                    filteredAssets = filteredAssets.Where(a => duplicates.Contains(a.ForeignId));
                    break;

                case 13:
                    filteredAssets = filteredAssets.Where(a => a.Backup);
                    break;

                case 14:
                    filteredAssets = filteredAssets.Where(a => !a.Backup);
                    break;

                case 15:
                    filteredAssets = filteredAssets.Where(a => a.AssetSource == Asset.Source.CustomPackage && !File.Exists(a.Location));
                    break;

                case 16:
                    filteredAssets = filteredAssets.Where(a => a.Exclude);
                    break;

            }
            if (_selectedMaintenance != 16) filteredAssets = filteredAssets.Where(a => !a.Exclude);

            // filter after maintenance selection to enable queries like "duplicate but only custom packages shown"
            switch (AssetInventory.Config.packagesListing)
            {
                case 1:
                    filteredAssets = filteredAssets.Where(a => a.AssetSource != Asset.Source.RegistryPackage);
                    break;

                case 2:
                    filteredAssets = filteredAssets.Where(a => a.AssetSource == Asset.Source.AssetStorePackage || (a.AssetSource == Asset.Source.RegistryPackage && a.ForeignId > 0));
                    break;

                case 3:
                    filteredAssets = filteredAssets.Where(a => a.AssetSource == Asset.Source.RegistryPackage);
                    break;

                case 4:
                    filteredAssets = filteredAssets.Where(a => a.AssetSource == Asset.Source.CustomPackage);
                    break;

                case 5:
                    filteredAssets = filteredAssets.Where(a => a.AssetSource == Asset.Source.Directory);
                    break;

                case 6:
                    filteredAssets = filteredAssets.Where(a => a.AssetSource == Asset.Source.Archive);
                    break;
            }

            if (!string.IsNullOrWhiteSpace(_assetSearchPhrase))
            {
                if (_assetSearchPhrase.StartsWith("~")) // exact mode
                {
                    string term = _assetSearchPhrase.Substring(1);
                    filteredAssets = filteredAssets.Where(a =>
                    {
                        string phrase = term.ToLowerInvariant();
                        return a.GetDisplayName().ToLowerInvariant().Contains(phrase) || (a.Description != null && a.Description.ToLowerInvariant().Contains(phrase));
                    });
                }
                else
                {
                    string[] fuzzyWords = _assetSearchPhrase.Split(' ');
                    foreach (string fuzzyWord in fuzzyWords.Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)))
                    {
                        if (fuzzyWord.StartsWith("+"))
                        {
                            filteredAssets = filteredAssets.Where(a =>
                            {
                                string phrase = fuzzyWord.Substring(1).ToLowerInvariant();
                                return a.GetDisplayName().ToLowerInvariant().Contains(phrase) || (a.Description != null && a.Description.ToLowerInvariant().Contains(phrase));
                            });
                        }
                        else if (fuzzyWord.StartsWith("-"))
                        {
                            filteredAssets = filteredAssets.Where(a =>
                            {
                                string phrase = fuzzyWord.Substring(1).ToLowerInvariant();
                                return !a.GetDisplayName().ToLowerInvariant().Contains(phrase) && (a.Description == null || !a.Description.ToLowerInvariant().Contains(phrase));
                            });
                        }
                        else
                        {
                            filteredAssets = filteredAssets.Where(a =>
                            {
                                string phrase = fuzzyWord.ToLowerInvariant();
                                return a.GetDisplayName().ToLowerInvariant().Contains(phrase) || (a.Description != null && a.Description.ToLowerInvariant().Contains(phrase));
                            });
                        }
                    }
                }
            }

            string[] lastGroups = Array.Empty<string>();
            int catIdx = 0;
            IOrderedEnumerable<AssetInfo> orderedAssets;
            switch (AssetInventory.Config.assetGrouping)
            {
                case 0: // none
                    orderedAssets = AddPackageOrdering(filteredAssets);
                    orderedAssets.ToList().ForEach(a => data.Add(a.WithTreeData(a.GetDisplayName(), a.AssetId)));
                    break;

                case 2: // category
                    orderedAssets = filteredAssets.OrderBy(a => a.GetDisplayCategory(), StringComparer.OrdinalIgnoreCase)
                        .ThenBy(a => a.GetDisplayName(), StringComparer.OrdinalIgnoreCase);

                    string[] noCat = {"-no category-"};
                    foreach (AssetInfo info in orderedAssets)
                    {
                        // create hierarchy
                        string[] cats = string.IsNullOrEmpty(info.GetDisplayCategory()) ? noCat : info.GetDisplayCategory().Split('/');

                        lastGroups = AddCategorizedItem(cats, lastGroups, data, info, ref catIdx);
                    }
                    break;

                case 3: // publisher
                    IOrderedEnumerable<AssetInfo> orderedAssetsPub = filteredAssets.OrderBy(a => a.GetDisplayPublisher(), StringComparer.OrdinalIgnoreCase)
                        .ThenBy(a => a.GetDisplayName(), StringComparer.OrdinalIgnoreCase);

                    string[] noPub = {"-no publisher-"};
                    foreach (AssetInfo info in orderedAssetsPub)
                    {
                        // create hierarchy
                        string[] pubs = string.IsNullOrEmpty(info.GetDisplayPublisher()) ? noPub : new[] {info.GetDisplayPublisher()};

                        lastGroups = AddCategorizedItem(pubs, lastGroups, data, info, ref catIdx);
                    }
                    break;

                case 4: // tags
                    List<Tag> tags = AssetInventory.LoadTags();
                    foreach (Tag tag in tags)
                    {
                        IOrderedEnumerable<AssetInfo> taggedAssets = filteredAssets
                            .Where(a => a.PackageTags != null && a.PackageTags.Any(t => t.Name == tag.Name))
                            .OrderBy(a => a.GetDisplayName(), StringComparer.OrdinalIgnoreCase);

                        string[] cats = {tag.Name};
                        foreach (AssetInfo info in taggedAssets)
                        {
                            // create hierarchy
                            lastGroups = AddCategorizedItem(cats, lastGroups, data, info, ref catIdx);
                        }
                    }

                    IOrderedEnumerable<AssetInfo> remainingAssets = filteredAssets
                        .Where(a => a.PackageTags == null || a.PackageTags.Count == 0)
                        .OrderBy(a => a.GetDisplayName(), StringComparer.OrdinalIgnoreCase);
                    string[] untaggedCat = {"-untagged-"};
                    foreach (AssetInfo info in remainingAssets)
                    {
                        lastGroups = AddCategorizedItem(untaggedCat, lastGroups, data, info, ref catIdx);
                    }
                    break;

                case 5: // state
                    IOrderedEnumerable<AssetInfo> orderedAssetsState = filteredAssets.OrderBy(a => a.OfficialState, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(a => a.GetDisplayName(), StringComparer.OrdinalIgnoreCase);

                    string[] noState = {"-no state-"};
                    foreach (AssetInfo info in orderedAssetsState)
                    {
                        // create hierarchy
                        string[] pubs = string.IsNullOrEmpty(info.OfficialState) ? noState : new[] {info.OfficialState};

                        lastGroups = AddCategorizedItem(pubs, lastGroups, data, info, ref catIdx);
                    }
                    break;

                case 6: // location
                    IOrderedEnumerable<AssetInfo> orderedAssetsLocation = filteredAssets.OrderBy(a => GetLocationDirectory(a.Location), StringComparer.OrdinalIgnoreCase)
                        .ThenBy(a => a.GetDisplayName(), StringComparer.OrdinalIgnoreCase);

                    string[] noLocation = {"-no location-"};
                    foreach (AssetInfo info in orderedAssetsLocation)
                    {
                        // create hierarchy
                        string[] pubs = string.IsNullOrEmpty(GetLocationDirectory(info.Location)) ? noLocation : new[] {GetLocationDirectory(info.Location)};

                        lastGroups = AddCategorizedItem(pubs, lastGroups, data, info, ref catIdx);
                    }
                    break;
            }

            AssetTreeModel.SetData(data, true);
            AssetTreeView.Reload();
            HandleAssetTreeSelectionChanged(AssetTreeView.GetSelection());

            _textureLoading2?.Cancel();
            _textureLoading2 = new CancellationTokenSource();
            AssetUtils.LoadTextures(data, _textureLoading2.Token);
        }

        private string GetLocationDirectory(string location)
        {
            if (string.IsNullOrWhiteSpace(location)) return null;
            return Path.GetDirectoryName(location);
        }

        private IOrderedEnumerable<AssetInfo> AddPackageOrdering(IEnumerable<AssetInfo> list)
        {
            IOrderedEnumerable<AssetInfo> result = null;
            if (!AssetInventory.Config.sortAssetsDescending)
            {
                switch (AssetInventory.Config.assetSorting)
                {
                    case 0:
                        result = list.OrderBy(a => a.GetDisplayName(), StringComparer.OrdinalIgnoreCase);
                        break;

                    case 1:
                        result = list.OrderBy(a => a.PurchaseDate);
                        break;

                    case 2:
                        result = list.OrderBy(a => a.LastRelease);
                        break;

                    case 3:
                        result = list.OrderBy(a => a.PackageSize);
                        break;

                    case 4:
                        result = list.OrderBy(a => a.Location).ThenBy(a => a.GetDisplayName(), StringComparer.OrdinalIgnoreCase);
                        break;

                    case 5:
                        result = list.OrderBy(a => a.Hotness);
                        break;
                }
            }
            else
            {
                switch (AssetInventory.Config.assetSorting)
                {
                    case 0:
                        result = list.OrderByDescending(a => a.GetDisplayName(), StringComparer.OrdinalIgnoreCase);
                        break;

                    case 1:
                        result = list.OrderByDescending(a => a.PurchaseDate);
                        break;

                    case 2:
                        result = list.OrderByDescending(a => a.LastRelease);
                        break;

                    case 3:
                        result = list.OrderByDescending(a => a.PackageSize);
                        break;

                    case 4:
                        result = list.OrderByDescending(a => a.Location).ThenByDescending(a => a.GetDisplayName(), StringComparer.OrdinalIgnoreCase);
                        break;

                    case 5:
                        result = list.OrderByDescending(a => a.Hotness);
                        break;
                }
            }
            if (result == null) result = list.OrderBy(a => a.LastRelease);

            return result.ThenBy(a => a.GetDisplayName(), StringComparer.OrdinalIgnoreCase);
        }

        private static string[] AddCategorizedItem(string[] cats, string[] lastCats, List<AssetInfo> data, AssetInfo info, ref int catIdx)
        {
            // find first difference to previous cat
            if (!ArrayUtility.ArrayEquals(cats, lastCats))
            {
                int firstDiff = 0;
                bool diffFound = false;
                for (int i = 0; i < Mathf.Min(cats.Length, lastCats.Length); i++)
                {
                    if (cats[i] != lastCats[i])
                    {
                        firstDiff = i;
                        diffFound = true;
                        break;
                    }
                }
                if (!diffFound) firstDiff = lastCats.Length;

                for (int i = firstDiff; i < cats.Length; i++)
                {
                    catIdx--;
                    AssetInfo catItem = new AssetInfo().WithTreeData(cats[i], catIdx, i);
                    data.Add(catItem);
                }
            }

            AssetInfo item = info.WithTreeData(info.GetDisplayName(), info.AssetId, cats.Length);
            data.Add(item);

            return cats;
        }

        private void OpenInPackageView(AssetInfo info)
        {
            _tab = 1;
            AssetTreeView.SetSelection(new[] {info.AssetId}, TreeViewSelectionOptions.RevealAndFrame);
            HandleAssetTreeSelectionChanged(AssetTreeView.GetSelection());
        }

        private void HandleAssetTreeSelectionChanged(IList<int> ids)
        {
            _selectedTreeAsset = null;
            _selectedTreeAssets = _selectedTreeAssets ?? new List<AssetInfo>();
            _selectedTreeAssets.Clear();

            if (ids.Count == 1 && ids[0] > 0)
            {
                _selectedTreeAsset = AssetTreeModel.Find(ids[0]);
                if (_selectedTreeAsset != null)
                {
                    // refresh immediately for single selections to have all buttons correct at once
                    _selectedTreeAsset.Refresh();
                    _selectedTreeAsset.PackageDownloader?.RefreshState();

                    // clear all existing media to conserve memory
                    AssetTreeModel.GetData().ForEach(d =>
                    {
                        d.AllMedia = null;
                        d.Media = null;
                    });
                    AssetInventory.LoadMedia(_selectedTreeAsset);
                }
            }

            // load all selected items but count each only once
            foreach (int id in ids)
            {
                GatherTreeChildren(id, _selectedTreeAssets, AssetTreeModel);
            }
            _selectedTreeAssets = _selectedTreeAssets.Distinct().ToList();

            _assetBulkTags.Clear();

            // initialize download status
            AssetInventory.RegisterSelection(_selectedTreeAssets);

            // merge tags
            _selectedTreeAssets.ForEach(info => info.PackageTags?.ForEach(t =>
            {
                if (!_assetBulkTags.ContainsKey(t.Name)) _assetBulkTags.Add(t.Name, new Tuple<int, Color>(0, t.GetColor()));
                _assetBulkTags[t.Name] = new Tuple<int, Color>(_assetBulkTags[t.Name].Item1 + 1, _assetBulkTags[t.Name].Item2);
            }));

            _assetTreeSelectionSize = _selectedTreeAssets.Sum(a => a.PackageSize);
            _assetTreeSelectionTotalCosts = _selectedTreeAssets.Sum(a => a.GetPrice());
            _assetTreeSelectionStoreCosts = _selectedTreeAssets.Where(a => a.AssetSource == Asset.Source.AssetStorePackage).Sum(a => a.GetPrice());
        }

        private void OnAssetTreeSelectionChanged(IList<int> ids)
        {
            _selectedMedia = 0;
            HandleAssetTreeSelectionChanged(ids);
        }

        private void OnAssetTreeDoubleClicked(int id)
        {
            if (id <= 0) return;

            AssetInfo info = AssetTreeModel.Find(id);
            OpenInSearch(info);
        }

        private int GetInspectorWidth()
        {
            return UIStyles.INSPECTOR_WIDTH * (AssetInventory.Config.expandPackageDetails && _tab == 1 ? 2 : 1);
        }
    }
}
