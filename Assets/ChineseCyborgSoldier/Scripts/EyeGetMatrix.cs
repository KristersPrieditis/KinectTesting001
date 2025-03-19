using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ChineseCyborgWarrior
{
    [ExecuteInEditMode]
    public sealed class EyeGetMatrix : MonoBehaviour
    {
        Renderer rend;
        public Transform head;
        private Dictionary<int, Material> mats;
        public Shader targetShader;
        private void Awake()
        {
            mats = new Dictionary<int, Material>();

            rend = GetComponent<Renderer>();
            for(int i = 0;i<rend.materials.Length;  i++)
            {
                if (rend.materials[i].shader.name == "Custom/Eye")
                {
                    //Debug.Log("Found Mat");
                    mats.Add(i, rend.materials[i]);
                }
                    

            }
        }
        
        private void SetMatParams(Material mat, int index)
        {
            if (mat == null)
            {

                return;
            }

            mat.SetMatrix("LocalToWorldMatrix_Inverse", head.localToWorldMatrix.inverse);
            mat.SetFloat("ScaleMul", transform.root.localScale.y * 3.266372f);

            rend.materials[index] = mat;
        }

        void Update()
        {
            foreach(var item in mats)
                SetMatParams(item.Value, item.Key);
        }
    }

}
