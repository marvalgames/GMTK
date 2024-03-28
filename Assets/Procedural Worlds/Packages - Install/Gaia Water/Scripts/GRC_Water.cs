#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif
using UnityEngine;

namespace Gaia
{
    public class GRC_Water : GaiaRuntimeComponent
    {
        private GameObject m_currentWaterPrefab;
        private GUIContent m_generalHelpLink;

        private GRC_WaterSettings m_waterSettings;
        public GRC_WaterSettings WaterSettings
        {
            get
            {
                if (m_waterSettings == null)
                {
                    m_waterSettings = GetWaterSettings();
                }
                return m_waterSettings;
            }
        }

        private GUIContent m_panelLabel;
        public override GUIContent PanelLabel
        {
            get
            {
                if (m_panelLabel == null || m_panelLabel.text == "")
                {
                    m_panelLabel = new GUIContent("Gaia Water", "Add a water surface & underwater effects at the sea level in your scene.");
                }
                return m_panelLabel;
            }
        }

        public bool m_VRShaderInitialized = false;

        public bool m_useVRWaterShader = false;
        private GUIContent m_useVRWaterShaderLabel;

        public override void Initialize()
        {
            m_orderNumber = 200;

            if (m_generalHelpLink == null || m_generalHelpLink.text == "")
            {
                m_generalHelpLink = new GUIContent("Gaia Water Module on Canopy", "Opens the Canopy Online Help Article for the Gaia Water Module");
            }
            if (WaterSettings != null)
            {
#if HDPipeline
                m_currentWaterPrefab = WaterSettings.m_HDRPWaterPrefab;
#elif UPPipeline
                m_currentWaterPrefab = WaterSettings.m_URPWaterPrefab;
#else
                m_currentWaterPrefab = WaterSettings.m_builtInWaterPrefab;
#endif
            }

            if (m_useVRWaterShaderLabel == null || m_useVRWaterShaderLabel.text == "")
            {
                m_useVRWaterShaderLabel = new GUIContent("Use VR/Mobile Shader", "Use a lightweight VR / Mobile water shader with less features, but better device compability. Depending on the target platform or if you are using the XR Management plugin in the project, Gaia will try to automatically select the right choice.");
            }
            if (!m_VRShaderInitialized)
            {
#if UNITY_EDITOR
                // Check if the current target platform in Unity Editor is mobile or VR
                if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android ||
                    UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS ||
                    XRPluginPackageCheck())
                {
                    m_useVRWaterShader = true;
                }
                else
                {
                    m_useVRWaterShader = false;
                }
                //remember that we initialized this setting already in this project, afterwards we respect the choice of the user if they decide to toggle it manually.
                m_VRShaderInitialized = true;
#endif
            }

        }

        public override void DrawUI()
        {
#if UNITY_EDITOR
            DisplayHelp("You can add an ocean water surface to your scene to simulate water at the sea level. This can be used to render an ocean or a lake in your scene. You will be able to customize the look of the water on the Water Surface Game Object.", m_generalHelpLink, "https://canopy.procedural-worlds.com/library/tools/gaia-pro-2021/written-articles/creating_runtime/runtime-module-gaia-water-r166/");

            bool originalGUIState = GUI.enabled;
            EditorGUI.BeginChangeCheck();
            {
#if UPPipeline
                m_useVRWaterShader = EditorGUILayout.Toggle(m_useVRWaterShaderLabel, m_useVRWaterShader);
#endif
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove"))
                {
                    RemoveFromScene();
                }
                GUILayout.Space(15);
                if (GUILayout.Button("Apply"))
                {
                    AddToScene();
                }
                GUILayout.EndHorizontal();
            }
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        public override void AddToScene()
        {
            //Remove any old versions first
            RemoveFromScene();
            GameObject gaiaRuntimeObject = GaiaUtils.GetRuntimeSceneObject(true);
            if (m_currentWaterPrefab != null)
            {
                GameObject newWaterGO = GameObject.Instantiate(m_currentWaterPrefab, Vector3.zero, new Quaternion(), gaiaRuntimeObject.transform);
                newWaterGO.name = newWaterGO.name.Replace("(Clone)", "");
                float seaLevel = GaiaSessionManager.GetSessionManager().GetSeaLevel();
                PWS_WaterSystem pws = newWaterGO.GetComponentInChildren<PWS_WaterSystem>();
                pws.transform.position = new Vector3(0f, seaLevel, 0f);

#if UPPipeline
                //Swap to the VR water shader if it is set up to do so
                Shader waterShader = Shader.Find("PWS/PW_Water_URP");
                if (m_useVRWaterShader)
                {
                    waterShader = Shader.Find("PWS/PW_Water_VR");
                }

                if (waterShader != null && !waterShader.name.Contains("Error"))
                {
                    foreach (Material mat in pws.m_waterMaterialInstances)
                    {
                        mat.shader = waterShader;
                    }
                }
#endif

            }

            GaiaUtils.RefreshPlayerSetup();
        }

        public override void RemoveFromScene()
        {
            GameObject gaiaWaterObject = GaiaUtils.GetWaterObject();
            if (gaiaWaterObject != null)
            {
                if (Application.isPlaying)
                {
                    GameObject.Destroy(gaiaWaterObject);
                }
                else
                {
                    GameObject.DestroyImmediate(gaiaWaterObject);
                }
            }
        }


        /// <summary>
        /// Return WaterSettings or null;
        /// </summary>
        /// <returns>Gaia settings or null if not found</returns>
        public static GRC_WaterSettings GetWaterSettings()
        {
            return GaiaUtils.GetAsset("Water Settings.asset", typeof(GRC_WaterSettings)) as GRC_WaterSettings;
        }

        /// <summary>
        /// Checks if the XR Plugin package is installed via reflection
        /// </summary>
        /// <returns></returns>
        private bool XRPluginPackageCheck()
        {
            //Look for assembly
#if UNITY_EDITOR
            var assemblies = CompilationPipeline.GetAssemblies();
            foreach (UnityEditor.Compilation.Assembly assembly in assemblies)
            {
                if (assembly.name.Contains("XR.Management"))
                {
                    //was found -> we are done
                    return true;
                }
            }
#endif
            return false;
        }

    }
}
