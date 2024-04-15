using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AssetInventory
{
    public sealed class AssetDownloader
    {
        private const int HEADER_CACHE_PERIOD = 600;

        public enum State
        {
            Initializing,
            Unavailable,
            Unknown,
            Downloading,
            Downloaded,
            UpdateAvailable
        }

        public DateTime lastRefresh = DateTime.MinValue;

        private AssetInfo _asset;
        private readonly AssetDownloadState _assetState = new AssetDownloadState();

        // caching
        private readonly TimedCache<int> _headerCache = new TimedCache<int>();

        public AssetDownloader(AssetInfo asset)
        {
            _asset = asset;
            AssetDownloaderUtils.OnDownloadFinished += OnDownloadFinished;
        }

        public AssetInfo GetAsset()
        {
            return _asset;
        }

        public void SetAsset(AssetInfo asset)
        {
            _asset = asset;
        }

        private void OnDownloadFinished(int foreignId)
        {
            if (_asset.ForeignId != foreignId) return;

            // update early in assumption it worked, reindexing will correct it if necessary
            _asset.Version = _asset.LatestVersion;
            DBAdapter.DB.Execute("update Asset set CurrentSubState=0, Version=? where Id=?", _asset.LatestVersion, _asset.AssetId);
            _asset.Refresh();
            _asset.PackageDownloader?.RefreshState();
        }

        public bool IsDownloadSupported()
        {
#if UNITY_2020_1_OR_NEWER
            return true;
#else
            // loading assembly will fail below 2020
            return false;
#endif
        }

        public AssetDownloadState GetState()
        {
            return _assetState;
        }

        public void RefreshState()
        {
            lastRefresh = DateTime.Now;
            _headerCache.Clear();

            CheckState();

            // TODO: do whenever file changes, not here?
            if (_assetState.state == State.Downloading)
            {
                _assetState.bytesTotal = _asset.PackageSize;
                if (_assetState.bytesTotal > 0) _assetState.progress = (float)_assetState.bytesDownloaded / _assetState.bytesTotal;
            }
        }

        private void CheckState()
        {
            string targetFile = _asset.GetCalculatedLocation();
            if (targetFile == null)
            {
                _assetState.SetState(State.Unknown);
                return;
            }

            string folder = Path.GetDirectoryName(targetFile);

            // see if any progress file is there
            FileInfo fileInfo = null;
            string downloadFile = Path.Combine(folder, $".{_asset.SafeName}-{_asset.ForeignId}.tmp");
            if (File.Exists(downloadFile))
            {
                fileInfo = new FileInfo(downloadFile);
            }
            else
            {
                string redownloadFile = Path.Combine(folder, $".{_asset.SafeName}-content__{_asset.ForeignId}.tmp");
                if (File.Exists(redownloadFile)) fileInfo = new FileInfo(redownloadFile);
            }
            if (fileInfo != null)
            {
                _assetState.SetState(State.Downloading);
                _assetState.bytesDownloaded = fileInfo.Length;
                _assetState.lastDownloadChange = fileInfo.LastWriteTime;
                return;
            }

            // give started downloads some time to settle
            if (_assetState.state == State.Downloading && DateTime.Now - _assetState.lastStateChange < TimeSpan.FromSeconds(5)) return;

            bool exists = File.Exists(targetFile);

            // check if package actually contains content for this asset
            if (exists)
            {
                int id = 0;
                if (_headerCache.TryGetValue(out int cachedId))
                {
                    id = cachedId;
                }
                else
                {
                    AssetHeader header = UnityPackageImporter.ReadHeader(targetFile, true);
                    if (header != null && int.TryParse(header.id, out int parsedId))
                    {
                        id = parsedId;
                    }
                    _headerCache.SetValue(id, TimeSpan.FromSeconds(HEADER_CACHE_PERIOD));
                }
                if (id > 0 && id != _asset.ForeignId)
                {
                    _assetState.SetState(State.Unavailable);
                    return;
                }
            }

            // update database location once file is downloaded
            string assetLocation = _asset.GetLocation(true);
            if (exists && string.IsNullOrEmpty(assetLocation))
            {
                _asset.Location = targetFile;
                _asset.Refresh();

                // work directly on db to make sure it's latest state
                DBAdapter.DB.Execute("update Asset set Location=? where Id=?", targetFile, _asset.AssetId);
                _assetState.SetState(State.Downloaded);
                return;
            }

            exists = exists || (!string.IsNullOrEmpty(assetLocation) && File.Exists(assetLocation));
            _assetState.SetState(exists ? (_asset.IsUpdateAvailable() ? State.UpdateAvailable : State.Downloaded) : State.Unavailable);
        }

        public void Download()
        {
            if (!IsDownloadSupported()) return;

            Assembly assembly = Assembly.Load("UnityEditor.CoreModule");
            Type asc = assembly.GetType("UnityEditor.AssetStoreUtils");
            MethodInfo download = asc.GetMethod("Download", BindingFlags.Public | BindingFlags.Static);
            Type downloadDone = assembly.GetType("UnityEditor.AssetStoreUtils+DownloadDoneCallback");
            Delegate onDownloadDone = Delegate.CreateDelegate(downloadDone, typeof (AssetDownloaderUtils), "OnDownloadDone");

            string json = new JObject(
                new JProperty("download", new JObject(
                    new JProperty("url", _asset.OriginalLocation),
                    new JProperty("key", _asset.OriginalLocationKey)
                ))).ToString();

            _assetState.SetState(State.Downloading);
            _assetState.bytesTotal = _asset.PackageSize;
            _assetState.bytesDownloaded = 0;

            string key = _asset.ForeignId.ToString();
            download?.Invoke(null, new object[]
            {
                key, _asset.OriginalLocation,
                new[] {_asset.SafePublisher, _asset.SafeCategory, _asset.SafeName},
                _asset.OriginalLocationKey, json, false, onDownloadDone
            });

            _asset.Refresh(true);
        }
    }

    public sealed class AssetDownloadState
    {
        public AssetDownloader.State state { get; private set; } = AssetDownloader.State.Initializing;
        public long bytesDownloaded;
        public long bytesTotal;
        public float progress;
        public DateTime lastStateChange;
        public DateTime lastDownloadChange;

        public void SetState(AssetDownloader.State newState)
        {
            state = newState;
            lastStateChange = DateTime.Now;
        }

        public override string ToString()
        {
            return $"Asset Download State '{state}' ({progress})";
        }
    }

    public static class AssetDownloaderUtils
    {
        public static Action<int> OnDownloadFinished;

        public static void OnDownloadDone(string package_id, string message, int bytes, int total)
        {
            if (message == "ok")
            {
                OnDownloadFinished?.Invoke(int.Parse(package_id));
                return;
            }
            Debug.LogError($"Error downloading asset {package_id}: {message}");
        }
    }
}
