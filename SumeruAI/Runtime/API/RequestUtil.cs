using System;
using System.Collections;
using SumeruAI.Core;

namespace SumeruAI.API
{

    [Serializable]
    public class LoginReqData
    {
        public string accessKey;

        public string secretKey;
    }

    [Serializable]
    public class LoginRepData : BaseResponse
    {
        public LoginRepBodyData data;
    }

    [Serializable]
    public class LoginRepBodyData
    {
        public string accessToken;

        public int expiresIn;
    }



    [Serializable]
    public class ATFReqData
    {
        /// <summary>
        /// start middle end
        /// </summary>
        public string status;

        /// <summary>
        /// audio base64
        /// </summary>
        public string dialogueBase64;

        /// <summary>
        /// pre audio base64
        /// </summary>
        public string lastDialogueBase64;

        /// <summary>
        /// 
        /// </summary>
        public string traceId;

    }

    [Serializable]
    public class ATFRepData : BaseResponse
    {
        public ATFRepBodyData data;
    }


    [Serializable]
    public class ATFRepBodyData
    {
        /// <summary>
        /// id
        /// </summary>
        public Int64 id;

        /// <summary>
        /// blendshape base64
        /// </summary>
        public string emoteKey;

        /// <summary>
        /// audio base64
        /// </summary>
        public string audioKey;

        /// <summary>
        /// fps
        /// </summary>
        public float fps;
    }


    [Serializable]
    public class ImageMattingRepData : BaseResponse
    {
        public long data;
    }

    [Serializable]
    public class ImageMattingResultRepData : BaseResponse
    {
        public ImageMattingResultBodyData data;
    }

    [Serializable]
    public class ImageMattingResultBodyData
    {
        public string id;

        /// <summary>
        /// 图片下载地址（临时链接，不定期失效或删除，请及时下载并自行保留）
        /// </summary>
        public string mattingImg;

        /// <summary>
        /// 状态：1 处理中，2 成功，3 失败
        /// </summary>
        public string status;
    }

    [Serializable]
    public class Mugen3DSqRepData : BaseResponse
    {
        public long data;
    }

    [Serializable]
    public class EmptyReqData
    {
    }

    [Serializable]
    public class Mugen3DHqRepData : BaseResponse
    {
        public Mugen3DHqRepBodyData data;
    }

    [Serializable]
    public class Mugen3DHqRepBodyData
    {
    }

    [Serializable]
    public class Mugen3DResultRepData : BaseResponse
    {
        public Mugen3DResultBodyData data;
    }

    [Serializable]
    public class Mugen3DResultBodyData
    {
        public long id;
        public string downloadSqModelLink;
        public string downloadHqModelLink;
        public string modelType;
        public string generationStatus;
        public string generationFailureReason;
        public int waitToCompletionSeconds;
        public string taskStartTime;
    }

}
