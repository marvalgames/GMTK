using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JBooth.MicroVerseCore.Browser
{
    public class ContentItemPopup : PopupWindowContent
    {
        private enum Feature
        {
            UpdateThumbnail,
            PingCollection,
            Rename,
            Close
        }

        private ContentBrowser browser;
        private PresetItem presetItem;

        private List<Feature> features;

        public ContentItemPopup(ContentBrowser browser, PresetItem presetItem)
        {
            this.browser = browser;
            this.presetItem = presetItem;

            // set features per preset content type
            features = new List<Feature>();

            switch(presetItem.collection.contentType)
            {
                case ContentType.Height:
                    //features.Add(Feature.UpdateThumbnail);
                    features.Add(Feature.PingCollection);
                    //features.Add(Feature.Rename);
                    features.Add(Feature.Close);
                    break;

                default:
                    features.Add(Feature.UpdateThumbnail);
                    features.Add(Feature.PingCollection);
                    features.Add(Feature.Rename);
                    features.Add(Feature.Close);
                    break;
            }
        }

        public override Vector2 GetWindowSize()
        {
            int menuRows = features.Count;
            return new Vector2(180, 21 * menuRows);
        }

        public override void OnGUI(Rect rect)
        {
            if (features.Contains(Feature.UpdateThumbnail))
            {
                if (GUILayout.Button("Update Thumbnail"))
                {
                    ThumbnailCreator.SaveIcon( presetItem);
                }
            }

            if (features.Contains(Feature.PingCollection))
            {
                if (GUILayout.Button("Ping Collection"))
                {
                    Ping();

                }
            }

            if (features.Contains(Feature.Rename))
            {
                if (GUILayout.Button("Rename"))
                {
                    Rename();
                }
            }

            if (features.Contains(Feature.Close))
            {
                if (GUILayout.Button("Close"))
                {
                    editorWindow.Close();
                }
            }
        }

        public override void OnOpen()
        {
        }

        public override void OnClose()
        {
        }



        void Rename()
        {
            if (presetItem == null)
                return;

            ContentData item = presetItem.content;

            if (item.prefab == null)
            {
                Debug.LogError("Prefab missing");
                return;
            }

            // close the current popup, we don't need it anymore
            // note: we can't close it later after PopupWindow.show ... for some reason the code execution stops there
            this.editorWindow.Close();

            // popup window parameters
            Vector2 position = Event.current.mousePosition;
            Rect popupRect = new Rect(position, Vector2.zero); // 2nd parameter is the offset from mouse position

            // show rename popup
            PresetItemPopup renamePopup = new PresetItemPopup(presetItem);
            UnityEditor.PopupWindow.Show(popupRect, renamePopup);

        }

        void Ping()
        {
            if (presetItem == null)
                return;

            EditorGUIUtility.PingObject(presetItem.collection);
        }
    }
}