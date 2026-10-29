// Loci contract v1 bridge. The class name is kept so the existing scene
// component keeps its serialized reference while the spike protocol is replaced.
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class SpikeBridge : MonoBehaviour
{
    private static readonly HashSet<string> ShellMessageTypes = new HashSet<string>
    {
        "session.start", "session.stop", "placement.confirm", "placement.reset",
        "topic.open", "topic.close", "settings.update", "diag.ping"
    };

    private static readonly HashSet<string> TopicIds = new HashSet<string>
    {
        "array_list", "stack", "queue", "linked_list", "functions", "loops",
        "selection", "memory"
    };

    public static event Action<JObject> ShellCommandReceived;
    public static event Action<string> MessageSentToShell;

    // Called by React Native: postMessage("LociBridge", "OnShellMessage", json).
    public void OnShellMessage(string json)
    {
        ReceiveFromShell(json);
    }

    // Shared entry point for the Editor fake shell.
    public void ReceiveFromShell(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[LociBridge] Ignored an empty shell message.");
            return;
        }

        JObject message;
        try
        {
            message = JObject.Parse(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LociBridge] Ignored malformed JSON: " + e.Message);
            return;
        }

        var version = (int?)message["v"];
        var id = (string)message["id"];
        var type = (string)message["type"];
        var payload = message["payload"] as JObject;

        if (version != 1 || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(type) || payload == null)
        {
            Debug.LogWarning("[LociBridge] Ignored a message with an invalid v1 envelope.");
            return;
        }

        if (!ShellMessageTypes.Contains(type))
        {
            Debug.LogWarning("[LociBridge] Ignored unknown shell message type: " + type);
            return;
        }

        if (!HasValidRequiredFields(type, payload))
        {
            Debug.LogWarning("[LociBridge] Ignored invalid payload for " + type + ".");
            return;
        }

        Debug.Log("[LociBridge] Shell -> Unity " + type + " (" + id + ")");
        ShellCommandReceived?.Invoke(message);

        if (type == "diag.ping")
        {
            SendMessageToShell("diag.pong", new JObject { ["nonce"] = payload["nonce"] }, id);
        }
    }

    private static bool HasValidRequiredFields(string type, JObject payload)
    {
        switch (type)
        {
            case "topic.open":
                var topicId = (string)payload["topicId"];
                return !string.IsNullOrWhiteSpace(topicId) && TopicIds.Contains(topicId);
            case "settings.update":
                var scale = (double?)payload["textScale"];
                return scale.HasValue && scale.Value > 0d;
            case "diag.ping":
                return payload["nonce"] != null && payload["nonce"].Type == JTokenType.String;
            default:
                return true;
        }
    }

    public static void SendMessageToShell(string type, JObject payload, string replyTo = null)
    {
        var message = new JObject
        {
            ["v"] = 1,
            ["id"] = "u-" + Guid.NewGuid().ToString("N"),
            ["type"] = type,
            ["payload"] = payload ?? new JObject()
        };
        if (!string.IsNullOrWhiteSpace(replyTo))
            message["replyTo"] = replyTo;

        var json = message.ToString(Newtonsoft.Json.Formatting.None);
        MessageSentToShell?.Invoke(json);
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var cls = new AndroidJavaClass("com.azesmwayreactnativeunity.ReactNativeUnityViewManager"))
                cls.CallStatic("sendMessageToMobileApp", json);
        }
        catch (Exception e)
        {
            Debug.LogError("[LociBridge] Could not send a message to the shell: " + e.Message);
        }
#else
        Debug.Log("[LociBridge] Unity -> Shell " + json);
#endif
    }
}
