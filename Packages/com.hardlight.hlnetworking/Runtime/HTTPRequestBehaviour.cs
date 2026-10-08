using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.Networking;

namespace Hardlight.Networking
{
    // Original HLNetworking.Runtime 0x02000005. Complete surviving owner and
    // natural callback/iterator flows inferred from the full ARM64/x86 ranges.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class HTTPRequestBehaviour
    {
        // Original 0x04000006 and setter 0x06000010. The getter was stripped.
        // C# requires an auto-property getter to emit the exact original backing
        // field name. This private getter is a compatibility-only source member;
        // its original visibility/body/identity are unknown and receive no credit.
        private static Uri LastUri { get; set; }

        // Original 0x02000006: numeric status membership, including Timeout zero.
        public enum Status
        {
            Success = 200,
            BadRequest = 400,
            Unauthorized = 401,
            Forbidden = 403,
            NotFound = 404,
            LoginTimeOut = 440,
            ServerError = 500,
            ServiceUnavailable = 503,
            UnknownError = 520,
            MalformedResponse = 998,
            ConnectionFailure = 999,
            Timeout = 0
        }

        // Original 0x02000007, .ctor 0x0600001d and Invoke 0x0600001e.
        // Standard C# delegate runtime methods are not additional original APIs.
        public delegate void JSONResponseCallback(Status status, JSONHashtable responseData = null);

        // 0x06000011. The real coroutine host returns a Coroutine reference.
        public static bool sendRequest(UnityWebRequest request, Action<UnityWebRequest> callback)
        {
            return Hardlight.Utils.CoroutineUtils.RunCoroutine(sendRequestInternal(request, callback)) != null;
        }

        // 0x06000012. No callback or request validation is introduced.
        public static bool post(Uri uri, object body, JSONResponseCallback callback, float timeout = 10f)
        {
            return post(uri, body, null, callback, timeout);
        }

        // 0x06000013 and original display class 0x02000008 (0x0600001f/20).
        public static bool post(Uri uri, object body, string accessToken, JSONResponseCallback callback, float timeout = 10f)
        {
            UnityWebRequest request = createPostRequest(uri, body, accessToken, timeout);
            if (request != null)
            {
                request.SetRequestHeader("Accept", "application/json");
                return sendRequest(request, _request =>
                {
                    LastUri = uri;
                    // The original callback uses the captured request, ignores
                    // _request, and disposes only after a successful callback.
                    handleResponse(request, callback);
                    request.Dispose();
                });
            }
            return false;
        }

        // 0x06000014; original iterator 0x02000009, six methods 0x06000021..26.
        // Early disposal does not dispose the request or invoke its callback.
        private static IEnumerator sendRequestInternal(UnityWebRequest request, Action<UnityWebRequest> callback)
        {
            yield return request.SendWebRequest();
            callback(request);
        }

        // 0x06000015. Narrowing is ordinary unchecked conversion; neither the
        // Unity request error flags nor a broader HTTP success range is consulted.
        private static Status decodeStatus(UnityWebRequest response)
        {
            if (response == null) return Status.ConnectionFailure;
            Status status = (Status)unchecked((int)response.responseCode);
            return Enum.IsDefined(typeof(Status), status) ? status : Status.UnknownError;
        }

        // 0x06000016. A successful callback must return before the table releases;
        // a parsed non-table object is not released on the malformed route.
        private static void handleResponse(UnityWebRequest request, JSONResponseCallback callback)
        {
            if (request != null && unchecked((int)request.responseCode) == 200)
            {
                JSONHashtable response = JSONSerializer.Decode(request.downloadHandler.text) as JSONHashtable;
                if (response != null)
                {
                    callback(Status.Success, response);
                    response.Release();
                    return;
                }
                callback(Status.MalformedResponse, null);
            }
            else callback(decodeStatus(request), null);
        }

        // 0x06000017. Dispatch ordering is original. Unsupported objects retain
        // the null request and fault when timeout is assigned; no fallback exists.
        private static UnityWebRequest createPostRequest(Uri uri, object body, string accessToken, float timeout = 10f)
        {
            UnityWebRequest request;
            if (body == null)
            {
                request = createPostRequest(uri, string.Empty);
            }
            else if (body is Hashtable) request = createPostRequest(uri, (Hashtable)body);
            else if (body is byte[]) request = createPostRequest(uri, (byte[])body);
            else if (body is string) request = createPostRequest(uri, (string)body);
            else if (body is Dictionary<string, string>) request = createPostRequest(uri, (Dictionary<string, string>)body);
            else if (body is IEnumerable) request = createPostRequest(uri, (IEnumerable)body);
            else request = null;

            if (accessToken != null && request != null) request.SetRequestHeader("Authorization", accessToken);
            // Exceptional float conversions are architecture-dependent in the
            // shipping native builds; this is the ordinary managed conversion.
            request.timeout = unchecked((int)timeout);
            return request;
        }

        // 0x06000018. Original external callee 0x29ada80 is PostWwwForm, followed
        // by the original application/text header, rather than JSON encoding.
        private static UnityWebRequest createPostRequest(Uri uri, string stringBody)
        {
            UnityWebRequest request = UnityWebRequest.PostWwwForm(uri, stringBody);
            request.SetRequestHeader("Content-Type", "application/text");
            return request;
        }

        // 0x06000019. Download allocation precedes upload allocation and request.
        private static UnityWebRequest createPostRequest(Uri uri, byte[] byteArrayBody)
        {
            DownloadHandlerBuffer download = new DownloadHandlerBuffer();
            UploadHandlerRaw upload = new UploadHandlerRaw(byteArrayBody);
            UnityWebRequest request = new UnityWebRequest(uri, "POST", download, upload);
            request.SetRequestHeader("Content-Type", "application/octet-stream");
            return request;
        }

        // 0x0600001a. The authored concatenation does not escape keys or values;
        // an empty dictionary passes a null string to the original Unity helper.
        private static UnityWebRequest createPostRequest(Uri uri, Dictionary<string, string> urlEncodedBody)
        {
            string stringBody = null;
            foreach (KeyValuePair<string, string> pair in urlEncodedBody)
            {
                string prefix = stringBody != null ? string.Concat(stringBody, "&") : null;
                stringBody = string.Concat(prefix, pair.Key, "=", pair.Value);
            }
            UnityWebRequest request = UnityWebRequest.PostWwwForm(uri, stringBody);
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            return request;
        }

        // 0x0600001b. Request construction and UTF8 retrieval precede JSON encode;
        // upload assignment precedes download allocation. No cleanup guard exists.
        private static UnityWebRequest createPostRequest(Uri uri, Hashtable jsonBody)
        {
            UnityWebRequest request = new UnityWebRequest(uri, "POST");
            Encoding encoding = Encoding.UTF8;
            byte[] bytes = encoding.GetBytes(JSONSerializer.Encode(jsonBody));
            request.uploadHandler = bytes.Length > 0 ? new UploadHandlerRaw(bytes) : null;
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        // 0x0600001c. The whole IEnumerable is serialized before sending.
        private static UnityWebRequest createPostRequest(Uri uri, IEnumerable jsonBody)
        {
            UnityWebRequest request = new UnityWebRequest(uri, "POST");
            Encoding encoding = Encoding.UTF8;
            byte[] bytes = encoding.GetBytes(JSONSerializer.Encode(jsonBody));
            request.uploadHandler = bytes.Length > 0 ? new UploadHandlerRaw(bytes) : null;
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }
    }
}
