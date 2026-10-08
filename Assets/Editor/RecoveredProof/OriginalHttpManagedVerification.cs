using System;
using System.Collections;
using System.Reflection;
using Hardlight.JSON;
using Hardlight.Networking;
using UnityEngine.Networking;

namespace ProjectLucid.Verification
{
internal static class OriginalHttpManagedVerification
{
    public static int RunNativeFreeBoundaries()
    {
        int checks = 0;
        Action<bool, string> check = (condition, detail) =>
        {
            ++checks;
            if (!condition) throw new Exception(detail);
        };
        int[] values = { 200, 400, 401, 403, 404, 440, 500, 503, 520, 998, 999, 0 };
        string[] names = { "Success", "BadRequest", "Unauthorized", "Forbidden", "NotFound", "LoginTimeOut", "ServerError", "ServiceUnavailable", "UnknownError", "MalformedResponse", "ConnectionFailure", "Timeout" };
        for (int i = 0; i < names.Length; ++i)
            check((int)Enum.Parse(typeof(HTTPRequestBehaviour.Status), names[i]) == values[i], "original status constant " + names[i]);

        Type owner = typeof(HTTPRequestBehaviour);
        MethodInfo decode = owner.GetMethod("decodeStatus", BindingFlags.NonPublic | BindingFlags.Static);
        check((HTTPRequestBehaviour.Status)decode.Invoke(null, new object[] { null }) == HTTPRequestBehaviour.Status.ConnectionFailure, "null status returns ConnectionFailure");
        int callbacks = 0;
        HTTPRequestBehaviour.Status observedStatus = 0;
        JSONHashtable observedData = new JSONHashtable();
        HTTPRequestBehaviour.JSONResponseCallback callback = (status, data) => { ++callbacks; observedStatus = status; observedData = data; };
        callback(HTTPRequestBehaviour.Status.MalformedResponse);
        check(callbacks == 1 && observedStatus == HTTPRequestBehaviour.Status.MalformedResponse && observedData == null, "genuine optional delegate argument null");
        MethodInfo response = owner.GetMethod("handleResponse", BindingFlags.NonPublic | BindingFlags.Static);
        response.Invoke(null, new object[] { null, callback });
        check(callbacks == 2 && observedStatus == HTTPRequestBehaviour.Status.ConnectionFailure && observedData == null, "null request takes mandatory failure callback");
        bool callbackFault = false;
        try { response.Invoke(null, new object[] { null, null }); }
        catch (TargetInvocationException ex) { callbackFault = ex.InnerException is NullReferenceException; }
        check(callbackFault, "missing callback faults without request or network call");

        MethodInfo iteratorFactory = owner.GetMethod("sendRequestInternal", BindingFlags.NonPublic | BindingFlags.Static);
        Action<UnityWebRequest> done = request => ++callbacks;
        IEnumerator iterator = (IEnumerator)iteratorFactory.Invoke(null, new object[] { null, done });
        check(callbacks == 2 && iterator.Current == null, "iterator creation does not send or call back");
        ((IDisposable)iterator).Dispose();
        check(callbacks == 2, "early iterator disposal has no callback");
        bool resetFault = false;
        try { iterator.Reset(); } catch (NotSupportedException) { resetFault = true; }
        check(resetFault, "original iterator reset unsupported");
        bool requestFault = false;
        try { iterator.MoveNext(); } catch (NullReferenceException) { requestFault = true; }
        check(requestFault && callbacks == 2, "null request faults at first MoveNext before any send/native call");

        FieldInfo backing = owner.GetField("<LastUri>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
        object previous = backing.GetValue(null);
        Uri uri = new Uri("https://example.invalid/original-native-free-fixture");
        try
        {
            owner.GetMethod("set_LastUri", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { uri });
            check(object.ReferenceEquals(uri, backing.GetValue(null)), "original private setter retains exact Uri reference");
        }
        finally { backing.SetValue(null, previous); }
        return checks;
    }
}

}
