using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace AssetInventory
{
    public abstract class AssetImporter : AssetProgress
    {
        protected static async Task RemovePersistentCacheEntry(Asset asset)
        {
            // remove old version first from cache if exists already
            if (asset.KeepExtracted)
            {
                string path = AssetInventory.GetMaterializedAssetPath(asset);
                if (Directory.Exists(path)) await IOUtils.DeleteFileOrDirectory(path);
            }
        }

        protected static void RemoveWorkFolder(Asset asset, string tempPath)
        {
            // remove files again, no need to wait
            if (!asset.KeepExtracted)
            {
                Task _ = Task.Run(() => Directory.Delete(tempPath, true));
            }
        }

        protected static Asset Fetch(Asset asset)
        {
            if (asset.AssetSource == Asset.Source.RegistryPackage)
            {
                return DBAdapter.DB.Find<Asset>(a => a.SafeName == asset.SafeName);
            }
            if (asset.AssetSource == Asset.Source.Archive)
            {
                return DBAdapter.DB.Find<Asset>(a => a.Location == asset.Location);
            }

            Asset result = null;

            // main index is location + foreign Id since Asset Store supports multiple versions under the same location potentially
            // cater for cases when folder capitalization changes due to metadata changes

            // use most specific data if available to differentiate between multi-version assets
            if (asset.ForeignId > 0 && !string.IsNullOrEmpty(asset.Location))
            {
                result = DBAdapter.DB.Table<Asset>()
                    .FirstOrDefault(a => a.ForeignId == asset.ForeignId && a.Location.ToLower() == asset.Location.ToLower());
            }

            // check for Id only if from Asset Store with no location yet
            if (result == null && asset.ForeignId > 0 && string.IsNullOrEmpty(asset.Location))
            {
                result = DBAdapter.DB.Table<Asset>()
                    .FirstOrDefault(a => a.ForeignId == asset.ForeignId && a.Location == null);
            }

            // check for location only if not from Asset Store
            if (result == null && asset.ForeignId <= 0 && !string.IsNullOrEmpty(asset.Location))
            {
                result = DBAdapter.DB.Table<Asset>()
                    .FirstOrDefault(a => a.Location.ToLower() == asset.Location.ToLower());
            }

            // check for Safe combination
            if (result == null && asset.ForeignId <= 0 && !string.IsNullOrEmpty(asset.SafeName))
            {
                result = DBAdapter.DB.Table<Asset>()
                    .Where(a => a.SafeName == asset.SafeName && a.SafeCategory == asset.SafeCategory && a.SafePublisher == asset.SafePublisher)
                    .OrderBy(a => a.OfficialState)
                    .LastOrDefault();
            }
            return result;
        }

        protected static bool Exists(AssetFile file)
        {
            if (string.IsNullOrEmpty(file.Guid))
            {
                return DBAdapter.DB.ExecuteScalar<int>("select count(*) from AssetFile where AssetId == ? and Path == ? limit 1", file.AssetId, file.Path) > 0;
            }
            return DBAdapter.DB.ExecuteScalar<int>("select count(*) from AssetFile where AssetId == ? && Guid == ? limit 1", file.AssetId, file.Guid) > 0;
        }

        protected static AssetFile Fetch(AssetFile file)
        {
            if (string.IsNullOrEmpty(file.Guid))
            {
                return DBAdapter.DB.Find<AssetFile>(f => f.AssetId == file.AssetId && f.Path == file.Path);
            }
            return DBAdapter.DB.Find<AssetFile>(f => f.AssetId == file.AssetId && f.Guid == file.Guid);
        }

        protected static AssetFile Fetch(AssetFile file, IEnumerable<AssetFile> existing)
        {
            if (string.IsNullOrEmpty(file.Guid))
            {
                return existing.FirstOrDefault(f => f.AssetId == file.AssetId && f.Path == file.Path);
            }
            return existing.FirstOrDefault(f => f.AssetId == file.AssetId && f.Guid == file.Guid);
        }

        protected static AssetFile Fetch(AssetFile file, Dictionary<string, List<AssetFile>> existingByGuid, Dictionary<(string, int), AssetFile> existingByPathAndAssetId)
        {
            if (string.IsNullOrEmpty(file.Guid))
            {
                if (existingByPathAndAssetId.TryGetValue((file.Path, file.AssetId), out AssetFile assetFile))
                {
                    return assetFile;
                }
            }
            else
            {
                if (existingByGuid.TryGetValue(file.Guid, out List<AssetFile> filesByGuid))
                {
                    return filesByGuid.FirstOrDefault(f => f.AssetId == file.AssetId);
                }
            }

            return null;
        }

        protected static Dictionary<string, List<AssetFile>> ToGuidDict(IEnumerable<AssetFile> files)
        {
            return files
                .Where(f => !string.IsNullOrEmpty(f.Guid))
                .GroupBy(f => f.Guid)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        public static Dictionary<int, AssetFile> ToIdDict(IEnumerable<AssetFile> files)
        {
            return files
                .GroupBy(f => f.Id)
                .ToDictionary(g => g.Key, g => g.First());
        }

        protected static Dictionary<(string Path, int AssetId), AssetFile> ToPathIdDict(IEnumerable<AssetFile> files)
        {
            return files
                .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(f => (f.First().Path, f.First().AssetId), f => f.First());
        }

        protected static void Persist(Asset asset)
        {
            if (asset.Id > 0)
            {
                DBAdapter.DB.Update(asset);
                return;
            }

            Asset existing = Fetch(asset);
            if (existing != null)
            {
                asset.Id = existing.Id;
                if (asset.ForeignId > 0) existing.ForeignId = asset.ForeignId;
                existing.Version = asset.Version;
                existing.SafeCategory = asset.SafeCategory;
                existing.SafePublisher = asset.SafePublisher;
                existing.CurrentState = asset.CurrentState;
                existing.AssetSource = asset.AssetSource;
                existing.PackageSize = asset.PackageSize;
                existing.Location = asset.Location;

                DBAdapter.DB.Update(existing);
            }
            else
            {
                DBAdapter.DB.Insert(asset);
            }
        }

        protected static void Persist(AssetFile file)
        {
            if (file.Id > 0)
            {
                DBAdapter.DB.Update(file);
                return;
            }

            AssetFile existing = Fetch(file);
            if (existing != null)
            {
                file.Id = existing.Id;
                DBAdapter.DB.Update(file);
            }
            else
            {
                DBAdapter.DB.Insert(file);
            }
        }

        protected static void UpdateOrInsert(Asset asset)
        {
            if (asset.Id > 0)
            {
                DBAdapter.DB.Update(asset);
            }
            else
            {
                DBAdapter.DB.Insert(asset);
            }
        }

        protected static async Task ProcessMediaAttributes(string file, AssetFile info, Asset asset)
        {
            // special processing for supported file types, from 2021.2+ more types can be supported
            #if UNITY_2021_2_OR_NEWER && UNITY_EDITOR_WIN
            if (ImageUtils.SYSTEM_IMAGE_TYPES.Contains(info.Type))
            #else
            if (info.Type == "png" || info.Type == "jpg")
            #endif
            {
                Tuple<int, int> dimensions = ImageUtils.GetDimensions(file);
                if (dimensions != null)
                {
                    info.Width = dimensions.Item1;
                    info.Height = dimensions.Item2;
                }
            }

            if (AssetInventory.IsFileType(info.FileName, "Audio"))
            {
                string contentFile = asset.AssetSource != Asset.Source.Directory ? await AssetInventory.EnsureMaterializedAsset(asset, info) : file;
                try
                {
                    AudioClip clip = await AssetUtils.LoadAudioFromFile(contentFile);
                    if (clip != null)
                    {
                        info.Length = clip.length;
                        clip.UnloadAudioData();
                    }
                }
                catch
                {
                    Debug.LogWarning($"Audio file '{Path.GetFileName(file)}' from {info} seems to have incorrect format.");
                }
            }
        }

        protected static FolderSpec GetDefaultImportSpec()
        {
            return new FolderSpec
            {
                pattern = "*.*",
                createPreviews = true,
                folderType = 1,
                scanFor = 6
            };
        }
    }
}
