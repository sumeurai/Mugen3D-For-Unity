using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;


namespace SumeruAI.API
{
    [CreateAssetMenu(fileName = "APISettingsConfig", menuName = "SumeruAI/API Settings Config")]

    public class APISettingsConfig : ScriptableObject
    {

        [SerializeField] private string accessKey;
        [SerializeField] private string secretKey;

        [SerializeField] private string baseUrl;
        [SerializeField] private string login;
        [SerializeField] private string imageMatting;
        [SerializeField] private string mugen3dSq;
        [SerializeField] private float mugen3dResultPollingIntervalSeconds = 120f;

        public string AccessKey
        {
            get { return accessKey; }
        }

        public string SecretKey
        {
            get { return secretKey; }
        }

        public string LoginUrl
        {
            get { return CombineUrl(baseUrl, login); }
        }

        public string ImageMattingUrl
        {
            get { return CombineUrl(baseUrl, imageMatting); }
        }

        public string GetImageMattingResultUrl(string id)
        {
            return CombineUrl(ImageMattingUrl, $"results/{id}");
        }


        public string Mugen3DSqUrl
        {
            get { return CombineUrl(baseUrl, mugen3dSq); }
        }

        public string GetMugen3DHqUrl(long id)
        {
            return CombineUrl(Mugen3DSqUrl, $"{id}/hq");
        }

        public string GetMugen3DResultUrl(long id)
        {
            return CombineUrl(baseUrl, $"v1/mugen-3d/results/{id}");
        }

        public float Mugen3DResultPollingIntervalSeconds
        {
            get { return mugen3dResultPollingIntervalSeconds <= 0f ? 120f : mugen3dResultPollingIntervalSeconds; }
        }

        private static APISettingsConfig instance;

        public static APISettingsConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<APISettingsConfig>("APISettingsConfig");

                    if (instance == null)
                    {
                        instance = CreateInstance<APISettingsConfig>();
                    }
                }

                return instance;
            }
        }



        public bool IsValid()
        {
            return !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey);
        }

        protected string CombineUrl(string url, string path)
        {
            if (string.IsNullOrEmpty(url)) return path;
            if (string.IsNullOrEmpty(path)) return url;

            return $"{url.TrimEnd('/')}/{path.TrimStart('/')}";
        }

#if UNITY_EDITOR

        public void SetAccessKeyAndSecretKeyFromEditor(string aKey, string sKey)
        {
            accessKey = aKey;
            secretKey = sKey;
            EditorUtility.SetDirty(this);
        }

#endif

    }
}
