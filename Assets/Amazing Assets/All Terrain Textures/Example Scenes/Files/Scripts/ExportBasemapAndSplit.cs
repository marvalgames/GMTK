using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using AmazingAssets.AllTerrainTextures;

namespace AmazingAssets.AllTerrainTextures.Examples
{
    public class ExportBasemapAndSplit : MonoBehaviour
    {
        public TerrainData terrainData;

        [Space(5)]
        public int splitCountX = 3;
        public int splitCountY = 3;

        [Space(5)]
        public int positionX = 0;
        public int positionY = 0;

        [Space]
        public bool enableHeightBasedBlend = false;
        [Range(0f, 1f)]
        public float heightTransition = 0;


        Texture2D basemapDiffuse;
        Texture2D basemapNormal;
                

        void OnDisable()
        {
            if (basemapDiffuse != null)
            {
                if (Application.isEditor)
                    GameObject.DestroyImmediate(basemapDiffuse);
                else
                    GameObject.Destroy(basemapDiffuse);
            }

            if (basemapNormal != null)
            {
                if (Application.isEditor)
                    GameObject.DestroyImmediate(basemapNormal);
                else
                    GameObject.Destroy(basemapNormal);
            }
        }

        void Start()
        {
            basemapDiffuse = terrainData.AllTerrainTextures(true, false).Basemap.Diffuse(0, splitCountX, splitCountY, positionX, positionY, enableHeightBasedBlend, heightTransition);
            basemapNormal = terrainData.AllTerrainTextures(true, false).Basemap.Normal(0, splitCountX, splitCountY, positionX, positionY, enableHeightBasedBlend, heightTransition);


            Material material = GetComponent<MeshRenderer>().material;

            material.SetTexture("_MainTex", basemapDiffuse);
            material.SetTexture("_BumpMap", basemapNormal);
        }
    }
}