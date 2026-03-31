using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;

namespace SumeruAI.Core
{

    [Serializable]
    public class BaseResponse
    {
        public int code;
        public string message;
        public string msg;
    }

    public class BaseHttpRequest
    {
        private static bool IsSuccessCode(int code)
        {
            return code == 0 || code == 200;
        }

        public async Task PostAsync<TRequest, TResponse>(
            string url,
            string accessToken,
            TRequest requestData,
            Action<TResponse> onSuccess,
            Action<string> onFail = null,
            bool writedata = false)
            where TResponse : BaseResponse
        {
            string jsonstr = JsonUtility.ToJson(requestData);

#if UNITY_EDITOR
            Debug.Log("URL: " + url);
            Debug.Log("JSON: " + jsonstr);

            // string filepath = Path.Combine(Application.dataPath.Replace("Assets", ""),"request.json");
            // File.WriteAllText(filepath,jsonstr);
#endif

            using (var uwr = new UnityWebRequest(url))
            {
                uwr.method = UnityWebRequest.kHttpVerbPOST;
                uwr.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(accessToken))
                {
                    uwr.SetRequestHeader("Authorization", accessToken);
                }

                byte[] bytes = Encoding.UTF8.GetBytes(jsonstr);
                uwr.uploadHandler = new UploadHandlerRaw(bytes);
                uwr.downloadHandler = new DownloadHandlerBuffer();

                var asyncOperation = uwr.SendWebRequest();

                while (!asyncOperation.isDone)
                {
                    await Task.Yield();
                }

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    onFail?.Invoke(uwr.error);
                    return;
                }

#if UNITY_EDITOR
                Debug.Log("Response: " + uwr.downloadHandler.text);
                // if (writedata)
                // {
                //     string filepath = Path.Combine(Application.dataPath.Replace("Assets", ""),"respone.json");
                //     File.WriteAllText(filepath, uwr.downloadHandler.text);
                // }
#endif

                TResponse response = JsonUtility.FromJson<TResponse>(uwr.downloadHandler.text);

                if (response != null)
                {
                    if (IsSuccessCode(response.code))
                    {
                        onSuccess?.Invoke(response);
                    }
                    else
                    {
                        string errorMsg = !string.IsNullOrEmpty(response.msg) ? response.msg : response.message;
                        onFail?.Invoke(errorMsg);
                    }
                }
                else
                {
                    onFail?.Invoke("Response parse failed");
                }
            }
        }


        public async Task GetAsync<TResponse>(
            string url,
            string accessToken,
            Action<TResponse> onSuccess,
            Action<string> onFail = null)
            where TResponse : BaseResponse
        {
#if UNITY_EDITOR
            Debug.Log("GET URL: " + url);
#endif

            using (var uwr = UnityWebRequest.Get(url))
            {
                if (!string.IsNullOrEmpty(accessToken))
                {
                    uwr.SetRequestHeader("Authorization", accessToken);
                }

                var asyncOperation = uwr.SendWebRequest();

                while (!asyncOperation.isDone)
                {
                    await Task.Yield();
                }

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    onFail?.Invoke(uwr.error);
                    return;
                }

#if UNITY_EDITOR
                Debug.Log("Response: " + uwr.downloadHandler.text);
#endif

                TResponse response = JsonUtility.FromJson<TResponse>(uwr.downloadHandler.text);

                if (response != null)
                {
                    if (IsSuccessCode(response.code))
                    {
                        onSuccess?.Invoke(response);
                    }
                    else
                    {
                        string errorMsg = !string.IsNullOrEmpty(response.msg) ? response.msg : response.message;
                        onFail?.Invoke(errorMsg);
                    }
                }
                else
                {
                    onFail?.Invoke("Response parse failed");
                }
            }
        }

        public async Task UploadImageAsync<TResponse>(
            string url,
            string accessToken,
            byte[] imageData,
            string fileName,
            Action<TResponse> onSuccess,
            Action<string> onFail = null)
            where TResponse : BaseResponse
        {
#if UNITY_EDITOR
            Debug.Log("Upload URL: " + url);
#endif

            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            formData.Add(new MultipartFormFileSection("file", imageData, fileName, "application/octet-stream"));

            using (var uwr = UnityWebRequest.Post(url, formData))
            {
                if (!string.IsNullOrEmpty(accessToken))
                {
                    uwr.SetRequestHeader("Authorization", accessToken);
                }

                var asyncOperation = uwr.SendWebRequest();

                while (!asyncOperation.isDone)
                {
                    await Task.Yield();
                }

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    onFail?.Invoke(uwr.error);
                    return;
                }

#if UNITY_EDITOR
                Debug.Log("Response: " + uwr.downloadHandler.text);
#endif

                TResponse response = JsonUtility.FromJson<TResponse>(uwr.downloadHandler.text);

                if (response != null)
                {
                    if (IsSuccessCode(response.code))
                    {
                        onSuccess?.Invoke(response);
                    }
                    else
                    {
                        string errorMsg = !string.IsNullOrEmpty(response.msg) ? response.msg : response.message;
                        onFail?.Invoke(errorMsg);
                    }
                }
                else
                {
                    onFail?.Invoke("Response parse failed");
                }
            }
        }

        public async Task DownloadFileAsync(
            string url,
            string accessToken,
            string savePath,
            Action<string> onSuccess,
            Action<string> onFail = null,
            Action<float> onProgress = null)
        {
#if UNITY_EDITOR
            Debug.Log("Download URL: " + url);
            Debug.Log("Save Path: " + savePath);
#endif

            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var uwr = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET))
            {
                uwr.downloadHandler = new DownloadHandlerFile(savePath);
                if (!string.IsNullOrEmpty(accessToken))
                {
                    uwr.SetRequestHeader("Authorization", accessToken);
                }

                var asyncOperation = uwr.SendWebRequest();

                while (!asyncOperation.isDone)
                {
                    onProgress?.Invoke(Mathf.Clamp01(uwr.downloadProgress));
                    await Task.Yield();
                }

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    TryDeletePartialFile(savePath);
                    onFail?.Invoke(uwr.error);
                    return;
                }

                onProgress?.Invoke(1f);
                onSuccess?.Invoke(savePath);
            }
        }

        private static void TryDeletePartialFile(string savePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(savePath) && File.Exists(savePath))
                {
                    File.Delete(savePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[API Warning] Failed to delete partial file: {ex.Message}");
            }
        }
    }
}
