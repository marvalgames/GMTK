using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using AmazingAssets.AllTerrainTextures;

namespace AmazingAssets.AllTerrainTextures.Examples
{
    public class ExportHeightmap : MonoBehaviour
    {
        public TerrainData terrainData;

        public bool extractHeightmapNormal;

        Texture2D heightmap;
        Texture2D heightmapNormal;


        void OnDisable()
        {
            if (heightmap != null)
            {
                if (Application.isEditor)
                    GameObject.DestroyImmediate(heightmap);
                else
                    GameObject.Destroy(heightmap);
            }

            if (heightmapNormal != null)
            {
                if (Application.isEditor)
                    GameObject.DestroyImmediate(heightmapNormal);
                else
                    GameObject.Destroy(heightmapNormal);
            }
        }

        void Start()
        {
            heightmap = terrainData.AllTerrainTextures(true, false).Generic.Heightmap(0, false, false);

            if(extractHeightmapNormal)
                heightmapNormal = terrainData.AllTerrainTextures(true, false).Generic.HeightmapNormal(0, false);


            Material material = GetComponent<MeshRenderer>().material;

            material.SetTexture("_Heightmap", heightmap);
            material.SetTexture("_BumpMap", heightmapNormal);
        }
    }
}