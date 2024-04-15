using System;
using System.Collections.Generic;
#if UNITY_2021_2_OR_NEWER && UNITY_EDITOR_WIN
using System.Drawing.Imaging;
#endif
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AssetInventory
{
    public sealed class MediaImporter : AssetImporter
    {
        private const double BREAK_INTERVAL = 0.1;

        public async Task Index(FolderSpec spec, Asset attachedAsset = null, bool storeRelativePath = false, bool actAsSubImporter = false)
        {
            if (!actAsSubImporter) ResetState(false);

            if (string.IsNullOrEmpty(spec.location)) return;
            if (!Directory.Exists(spec.GetLocation(true))) return;

            List<string> searchPatterns = new List<string>();
            List<string> types = new List<string>();
            switch (spec.scanFor)
            {
                case 0:
                    types.AddRange(new[] {"Audio", "Images", "Models"});
                    break;

                case 2:
                    types.Add("Audio");
                    break;

                case 3:
                    types.Add("Images");
                    break;

                case 4:
                    types.Add("Models");
                    break;

                case 6:
                    if (!string.IsNullOrWhiteSpace(spec.pattern)) searchPatterns.AddRange(spec.pattern.Split(';'));
                    break;
            }

            // load existing for orphan checking and caching 
            string previewFolder = AssetInventory.GetPreviewFolder();
            List<string> fileTypes = new List<string>();
            types.ForEach(t => fileTypes.AddRange(AssetInventory.TypeGroups[t]));
            List<AssetFile> existing = DBAdapter.DB.Table<AssetFile>()
                .Where(af => fileTypes.Contains(af.Type))
                .Where(af => af.SourcePath.StartsWith(spec.location)).ToList();

            // clean up existing
            if (spec.removeOrphans)
            {
                foreach (AssetFile file in existing)
                {
                    if (!File.Exists(file.GetSourcePath(true)))
                    {
                        // TODO: rethink if relative
                        Debug.Log($"Removing orphaned entry from index: {file.SourcePath}");
                        DBAdapter.DB.Delete<AssetFile>(file.Id);

                        if (File.Exists(file.GetPreviewFile(previewFolder))) File.Delete(file.GetPreviewFile(previewFolder));
                    }
                }
            }

            // scan for new files
            types.ForEach(t => searchPatterns.AddRange(AssetInventory.TypeGroups[t].Select(ext => $"*.{ext}")));
            string[] files = IOUtils.GetFiles(spec.GetLocation(true), searchPatterns, SearchOption.AllDirectories).ToArray();
            int fileCount = files.Length;
            SubCount = fileCount;
            if (!actAsSubImporter) MainProgress = 1; // small hack to trigger UI update in the end
            if (spec.createPreviews) PreviewGenerator.Init(fileCount);

            int progressId = MetaProgress.Start(actAsSubImporter ? "Updating files index" : "Updating media files index");

            if (attachedAsset == null)
            {
                if (spec.attachToPackage)
                {
                    attachedAsset = DBAdapter.DB.Find<Asset>(a => a.SafeName == spec.location);
                    if (attachedAsset == null)
                    {
                        attachedAsset = new Asset();
                        attachedAsset.SafeName = spec.GetLocation(true);
                        attachedAsset.DisplayName = Path.GetFileNameWithoutExtension(spec.GetLocation(true));
                        attachedAsset.AssetSource = Asset.Source.Directory;
                        Persist(attachedAsset);
                    }
                }
                else
                {
                    // use generic catch-all package
                    attachedAsset = DBAdapter.DB.Find<Asset>(a => a.SafeName == Asset.NONE);
                    if (attachedAsset == null)
                    {
                        attachedAsset = Asset.GetNoAsset();
                        Persist(attachedAsset);
                    }
                }
            }

            // cache
            int specLength = spec.GetLocation(true).Length + 1;
            Dictionary<string, List<AssetFile>> guidDict = ToGuidDict(existing);
            Dictionary<(string, int), AssetFile> pathIdDict = ToPathIdDict(existing);

            // do actual indexing
            double nextBreak = 0;
            for (int i = 0; i < files.Length; i++)
            {
                if (CancellationRequested) break;
                if (EditorApplication.timeSinceStartup > nextBreak)
                {
                    nextBreak = EditorApplication.timeSinceStartup + BREAK_INTERVAL;
                    await Task.Yield(); // let editor breath in case many files are already indexed
                    await Cooldown.Do();
                }

                string file = files[i];
                if (file.Contains("__MACOSX")) continue; // skip macosx resource fork folders
                string type = IOUtils.GetExtensionWithoutDot(file).ToLowerInvariant();
                if (type == "meta") continue; // never index .meta files

                MetaProgress.Report(progressId, i + 1, fileCount, file);
                CurrentSub = file;
                SubProgress = i + 1;

                AssetFile af = new AssetFile();
                af.AssetId = attachedAsset.Id;
                af.SourcePath = AssetInventory.MakeRelative(file);
                af.Path = storeRelativePath ? file.Substring(specLength) : af.SourcePath;

                string metaFile = $"{file}.meta";
                if (File.Exists(metaFile)) af.Guid = AssetUtils.ExtractGuidFromFile(metaFile);

                AssetFile existingAf = Fetch(af, guidDict, pathIdDict);
                if (existingAf != null && !spec.checkSize)
                {
                    // skip if already indexed and size check is disabled as it will slow down the process especially on dropbox folders significantly
                    continue;
                }

                // check if file is still there, there are cases (e.g. .bundle) which can disappear
                if (!File.Exists(file))
                {
                    Debug.LogWarning($"File '{file}' disappeared, skipping");
                    continue;
                }
                try
                {
                    FileInfo fileInfo = new FileInfo(file);
                    fileInfo.Refresh(); // otherwise can cause sporadic FileNotFound exceptions
                    long size = fileInfo.Length;

                    // reindex if file size changed
                    if (existingAf != null)
                    {
                        if (existingAf.Size == size) continue;

                        // make sure new changes carry over
                        existingAf.SourcePath = af.SourcePath;
                        existingAf.Path = af.Path;
                        if (!string.IsNullOrWhiteSpace(af.Guid)) existingAf.Guid = af.Guid;

                        af = existingAf;
                    }

                    CurrentSub = file + " (" + EditorUtility.FormatBytes(size) + ")";
                    await Task.Yield(); // let editor breath
                    MemoryObserver.Do(size);

                    af.FileName = Path.GetFileName(af.SourcePath);
                    af.Size = size;
                    af.Type = type;
                    if (AssetInventory.Config.gatherExtendedMetadata)
                    {
                        await ProcessMediaAttributes(file, af, attachedAsset); // must be run on main thread
                    }
                    Persist(af);
                }
                catch (Exception e)
                {
                    Debug.LogError($"File '{file}' could not be indexed: {e.Message}");
                }

                if (spec.createPreviews && PreviewGenerator.IsPreviewable(af.FileName, false))
                {
                    // TODO: use original preview pipeline as well or integrate image shortcut

                    string previewFile = af.GetPreviewFile(previewFolder);
                    string sourceFile = af.GetSourcePath(true);
                    bool legacyPreviews = true;

                    #if UNITY_2021_2_OR_NEWER && UNITY_EDITOR_WIN
                    if (ImageUtils.SYSTEM_IMAGE_TYPES.Contains(af.Type))
                    {
                        // scale up preview already during import
                        ImageUtils.ResizeImage(sourceFile, previewFile, AssetInventory.Config.upscaleSize, !AssetInventory.Config.upscaleLossless, ImageFormat.Png);
                        PreviewImporter.StorePreviewResult(new PreviewRequest {DestinationFile = previewFile, Id = af.Id, Icon = Texture2D.grayTexture, SourceFile = sourceFile});
                        af.PreviewState = AssetFile.PreviewOptions.Custom;
                        legacyPreviews = false;
                    }
                    #endif
                    if (legacyPreviews)
                    {
                        // let Unity generate a preview for whitelisted types (CS and ASMDEF will trigger recompile and fail the indexer) 
                        PreviewGenerator.RegisterPreviewRequest(af.Id, sourceFile, previewFile, PreviewImporter.StorePreviewResult);

                        // from time to time store the previews in case something goes wrong
                        PreviewGenerator.EnsureProgress();
                        if (PreviewGenerator.ActiveRequestCount() > 100)
                        {
                            CurrentSub = "Generating preview images...";
                            await PreviewGenerator.ExportPreviews(10);
                        }
                    }
                }
            }
            if (spec.createPreviews)
            {
                CurrentSub = "Finalizing preview images...";
                await PreviewGenerator.ExportPreviews();
                PreviewGenerator.Clear();
            }

            if (attachedAsset.SafeName != Asset.NONE)
            {
                // update date
                attachedAsset = Fetch(attachedAsset);
                attachedAsset.LastRelease = DateTime.Now;
                attachedAsset.Location = spec.GetLocation(false);
                Persist(attachedAsset);
            }

            MetaProgress.Remove(progressId);
            if (!actAsSubImporter) ResetState(true);
        }
    }
}
