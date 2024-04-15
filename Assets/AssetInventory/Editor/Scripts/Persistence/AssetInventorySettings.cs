using System;
using System.Collections.Generic;

namespace AssetInventory
{
    [Serializable]
    public sealed class AssetInventorySettings
    {
        public int searchType;
        public int searchField;
        public int sortField;
        public bool sortDescending;
        public int maxResults = 5;
        public int maxResultsLimit = 10000;
        public int timeout = 20;
        public int tileText;
        public bool allowEasyMode = true;
        public bool autoPlayAudio = true;
        public bool loopAudio;
        public bool pingSelected = true;
        public bool doubleClickImport;
        public bool groupLists = true;
        public bool autoHideSettings = true;
        public bool keepAutoDownloads;
        public bool limitAutoDownloads;
        public int downloadLimit = 500;
        public bool searchAutomatically = true;
        public int previewVisibility;
        public int tileSize = 150;
        public float searchDelay = 0.3f;
        public float hueRange = 10f;
        public string excludedExtensions = "asset;json;txt;cs;md;uss;asmdef;ttf;uxml;editorconfig;signature;yml;cginc;gitattributes;release;collabignore;suo";

        public float rowHeightMultiplier = 1.1f;
        public int mediaHeight = 350;
        public int mediaThumbnailWidth = 120;
        public int mediaThumbnailHeight = 75;
        public int currency; // 0 - EUR, 1 - USD, 2 - CYN

        public bool showSearchFilterBar;
        public bool showSearchDetailsBar = true;
        public bool filterOnlyIfBarVisible;
        public bool showPackageFilterBar;
        public bool expandPackageDetails;
        public bool showDetailFilters = true;
        public bool showSavedSearches = true;
        public bool showIndexLocations = true;
        public bool showIndexingSettings;
        public bool showImportSettings;
        public bool showBackupSettings;
        public bool showPreviewSettings;
        public bool showAdvancedSettings;
        public bool showHints = true;

        public bool indexAssetStore = true;
        public bool indexPackageCache = true;
        public int assetCacheLocationType; // 0 = auto, 1 = custom
        public string assetCacheLocation;
        public int packageCacheLocationType; // 0 = auto, 1 = custom
        public string packageCacheLocation;
        public bool downloadAssets = true;
        public bool gatherExtendedMetadata = true;
        public bool extractPreviews = true;
        public bool extractColors;
        public bool extractAudioColors;
        public bool excludeByDefault;
        public bool convertToPipeline;
        public bool indexAssetPackageContents = true;
        public bool indexPackageContents;
        public bool showIconsForMissingPreviews = true;
        public bool importPackageKeywordsAsTags;
        public bool storeUnityLocationsRelative;
        public string customStorageLocation;
        public bool showCustomPackageUpdates;
        public bool showIndirectPackageUpdates;

        public bool upscalePreviews = true;
        public bool upscaleLossless = true;
        public int upscaleSize = 256;

        public bool hideAdvanced = true;
        public bool useCooldown = true;
        public int cooldownInterval = 10; // minutes
        public int cooldownDuration = 60; // seconds
        public long memoryLimit = (1024 * 1024) * 1000; // every X megabytes

        public bool createBackups;
        public bool backupByDefault;
        public bool onlyLatestPatchVersion = true;
        public int backupsPerAsset = 5;
        public string backupFolder;
        public string cacheFolder;
        public string exportFolder;
        public string exportFolder2;

        public int importDestination;
        public int importStructure;
        public string importFolder;

        public int assetSorting;
        public bool sortAssetsDescending;
        public int assetGrouping;
        public int assetDeprecation;
        public int packagesListing = 1; // only assets per default
        public int observationSpeed = 5;

        public ulong statsImports;

        public List<FolderSpec> folders = new List<FolderSpec>();
        public List<SavedSearch> searches = new List<SavedSearch>();
    }
}
