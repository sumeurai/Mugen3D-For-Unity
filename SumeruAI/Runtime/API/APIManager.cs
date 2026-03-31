using SumeruAI.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


namespace SumeruAI.API
{
    public class APIManager : MonoBehaviour
    {
        private static APIManager instance;

        public static APIManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject o = new GameObject("APIManager");
                    instance = o.AddComponent<APIManager>();
                }
                return instance;
            }
        }

        [SerializeField] private string accessToken;

        public string AccessToken
        {
            get
            {
                return accessToken;
            }
            private set
            {
                accessToken = value;
            }
        }


        private BaseHttpRequest httpRequest;

        public BaseHttpRequest HttpRequest
        {
            get
            {
                if (httpRequest == null)
                {
                    httpRequest = new BaseHttpRequest();
                }
                return httpRequest;
            }
        }


        private void Awake()
        {
            DontDestroyOnLoad(this);
        }


        public async Task<TRep> RequestAsync<TReq, TRep>(
            string url,
            TReq requestData,
            Action<TRep> onSuccess = null,
            Action<string> onError = null)
            where TRep : BaseResponse
        {
            try
            {
                string token = url == APISettingsConfig.Instance.LoginUrl ? "" : AccessToken;

                TRep response = null;
                await HttpRequest.PostAsync<TReq, TRep>(
                    url,
                    token,
                    requestData,
                    (data) =>
                    {
                        response = data;
                        onSuccess?.Invoke(data);
                    },
                    (error) =>
                    {
                        Debug.LogError($"[API Error] {url}: {error}");
                        onError?.Invoke(error);
                    }
                );

                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API Exception] {url}: {ex.Message}");
                onError?.Invoke(ex.Message);
                return null;
            }
        }

        public void Request<TReq, TRep>(
            string url,
            TReq requestData,
            Action<TRep> onSuccess = null,
            Action<string> onError = null)
            where TRep : BaseResponse
        {
            _ = RequestAsync<TReq, TRep>(url, requestData, onSuccess, onError);
        }



        public void Login(Action successAction = null)
        {
            if (!APISettingsConfig.Instance.IsValid())
            {
                Debug.LogError("[API Error] accesskey or secretkey is empty!");
                return;
            }

            LoginReqData reqData = new LoginReqData();
            reqData.accessKey = APISettingsConfig.Instance.AccessKey;
            // reqData.secretKey = ComputeStringMD5(APISettingsConfig.Instance.SecretKey);
            reqData.secretKey = APISettingsConfig.Instance.SecretKey;

            Request<LoginReqData, LoginRepData>(APISettingsConfig.Instance.LoginUrl,reqData, (repData) =>
            {
                AccessToken = repData.data.accessToken;
                successAction?.Invoke();
            });
        }


        public async Task<TRep> RequestGetAsync<TRep>(
            string url,
            Action<TRep> onSuccess = null,
            Action<string> onError = null)
            where TRep : BaseResponse
        {
            try
            {
                TRep response = null;
                await HttpRequest.GetAsync<TRep>(
                    url,
                    AccessToken,
                    (data) =>
                    {
                        response = data;
                        onSuccess?.Invoke(data);
                    },
                    (error) =>
                    {
                        Debug.LogError($"[API Error] {url}: {error}");
                        onError?.Invoke(error);
                    }
                );

                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API Exception] {url}: {ex.Message}");
                onError?.Invoke(ex.Message);
                return null;
            }
        }

        public async Task<TRep> RequestUploadAsync<TRep>(
            string url,
            byte[] imageData,
            string fileName,
            Action<TRep> onSuccess = null,
            Action<string> onError = null)
            where TRep : BaseResponse
        {
            try
            {
                TRep response = null;
                await HttpRequest.UploadImageAsync<TRep>(
                    url,
                    AccessToken,
                    imageData,
                    fileName,
                    (data) =>
                    {
                        response = data;
                        onSuccess?.Invoke(data);
                    },
                    (error) =>
                    {
                        Debug.LogError($"[API Error] {url}: {error}");
                        onError?.Invoke(error);
                    }
                );

                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API Exception] {url}: {ex.Message}");
                onError?.Invoke(ex.Message);
                return null;
            }
        }

        public void UploadImageMatting(
            byte[] imageData,
            string fileName,
            Action<ImageMattingRepData> onSuccess = null,
            Action<string> onError = null)
        {
            _ = RequestUploadAsync<ImageMattingRepData>(
                APISettingsConfig.Instance.ImageMattingUrl,
                imageData,
                fileName,
                onSuccess,
                onError
            );
        }

        public void GetImageMattingResult(
            string id,
            Action<ImageMattingResultRepData> onSuccess = null,
            Action<string> onError = null)
        {
            _ = RequestGetAsync<ImageMattingResultRepData>(
                APISettingsConfig.Instance.GetImageMattingResultUrl(id),
                onSuccess,
                onError
            );
        }

        public void SubmitMugen3DSq(
            byte[] imageData,
            string fileName,
            Action<Mugen3DSqRepData> onSuccess = null,
            Action<string> onError = null)
        {
            _ = RequestUploadAsync<Mugen3DSqRepData>(
                APISettingsConfig.Instance.Mugen3DSqUrl,
                imageData,
                fileName,
                onSuccess,
                onError
            );
        }

        public void SubmitMugen3DHq(
            long id,
            Action<Mugen3DHqRepData> onSuccess = null,
            Action<string> onError = null)
        {
            Request<EmptyReqData, Mugen3DHqRepData>(
                APISettingsConfig.Instance.GetMugen3DHqUrl(id),
                new EmptyReqData(),
                onSuccess,
                onError
            );
        }

        public void GetMugen3DResult(
            long id,
            Action<Mugen3DResultRepData> onSuccess = null,
            Action<string> onError = null)
        {
            _ = RequestGetAsync<Mugen3DResultRepData>(
                APISettingsConfig.Instance.GetMugen3DResultUrl(id),
                onSuccess,
                onError
            );
        }

        public async Task<string> DownloadFileAsync(
            string url,
            string savePath,
            Action<string> onSuccess = null,
            Action<string> onError = null,
            Action<float> onProgress = null,
            bool useAccessToken = false)
        {
            try
            {
                string token = useAccessToken ? AccessToken : string.Empty;
                string resultPath = null;
                await HttpRequest.DownloadFileAsync(
                    url,
                    token,
                    savePath,
                    (path) =>
                    {
                        resultPath = path;
                        onSuccess?.Invoke(path);
                    },
                    (error) =>
                    {
                        Debug.LogError($"[API Error] Download {url}: {error}");
                        onError?.Invoke(error);
                    },
                    onProgress
                );

                return resultPath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API Exception] Download {url}: {ex.Message}");
                onError?.Invoke(ex.Message);
                return null;
            }
        }

        public void DownloadFile(
            string url,
            string savePath,
            Action<string> onSuccess = null,
            Action<string> onError = null,
            Action<float> onProgress = null,
            bool useAccessToken = false)
        {
            _ = DownloadFileAsync(url, savePath, onSuccess, onError, onProgress, useAccessToken);
        }


        public string ComputeStringMD5(string s)
        {
            // Create MD5 hash creator
            MD5 hashCreator = MD5.Create();

            // Convert the input string to a byte array
            byte[] data = hashCreator.ComputeHash(Encoding.UTF8.GetBytes(s));

            // Create a StringBuilder to collect the bytes and create a string
            StringBuilder stringBuilder = new StringBuilder();

            // Loop through each byte of the hashed data and format each one as a hexadecimal string
            for (int i = 0; i < data.Length; i++)
            {
                stringBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string
            return stringBuilder.ToString();
        }



    }
}
