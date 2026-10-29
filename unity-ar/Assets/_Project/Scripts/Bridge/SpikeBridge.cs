// Loci contract v1 bridge. The class name is kept so the existing scene
// component keeps its serialized reference while the protocol is typed.
using System;
using System.Collections.Generic;
using Loci.Bridge;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

public sealed class SpikeBridge : MonoBehaviour
{
    private static readonly JsonSerializerSettings IncomingSerializerSettings = new JsonSerializerSettings
    {
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    private static readonly JsonSerializerSettings OutgoingSerializerSettings = new JsonSerializerSettings
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore
    };

    public static event Action<LociMessageEnvelope, ILociMessagePayload> ShellCommandReceived;
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

        JObject rawMessage;
        try
        {
            rawMessage = JObject.Parse(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LociBridge] Ignored malformed JSON: " + e.Message);
            return;
        }

        LogUnknownFields(rawMessage, typeof(LociMessageEnvelope), "envelope");

        LociMessageEnvelope message;
        try
        {
            message = rawMessage.ToObject<LociMessageEnvelope>(JsonSerializer.Create(IncomingSerializerSettings));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LociBridge] Ignored an invalid message envelope: " + e.Message);
            return;
        }

        if (message == null)
        {
            Debug.LogWarning("[LociBridge] Ignored an empty deserialized envelope.");
            return;
        }
        if (!message.IsValid(out var envelopeError))
        {
            Debug.LogWarning("[LociBridge] Ignored an invalid v1 envelope: " + envelopeError);
            return;
        }

        if (!LociMessageTypes.IsShellToUnity(message.Type))
        {
            if (LociMessageModelRegistry.PayloadTypeFor(message.Type) == null)
                Debug.LogWarning("[LociBridge] Ignored unknown shell message type: " + message.Type);
            else
                Debug.LogWarning("[LociBridge] Ignored message sent in the wrong direction: " + message.Type);
            return;
        }

        var payloadType = LociMessageModelRegistry.PayloadTypeFor(message.Type);
        ILociMessagePayload payload;
        try
        {
            LogUnknownFields((JObject)message.Payload, payloadType, message.Type + " payload");
            payload = message.Payload.ToObject(payloadType, JsonSerializer.Create(IncomingSerializerSettings)) as ILociMessagePayload;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LociBridge] Ignored invalid payload for " + message.Type + ": " + e.Message);
            return;
        }

        if (payload == null)
        {
            Debug.LogWarning("[LociBridge] Ignored invalid payload for " + message.Type + ": no matching payload model.");
            return;
        }
        if (!payload.IsValid(out var payloadError))
        {
            Debug.LogWarning("[LociBridge] Ignored invalid payload for " + message.Type + ": " + payloadError);
            return;
        }

        var topicOpen = payload as TopicOpenPayload;
        if (topicOpen != null && topicOpen.ActivityId != null &&
            !LociTopicCatalog.IsActivityIdAllowed(topicOpen.TopicId, topicOpen.ActivityId))
        {
            Debug.LogWarning("[LociBridge] Ignored unknown activity ID '" + topicOpen.ActivityId + "' for topic " + topicOpen.TopicId + ".");
            return;
        }

        Debug.Log("[LociBridge] Shell -> Unity " + message.Type + " (" + message.Id + ")");
        ShellCommandReceived?.Invoke(message, payload);

        var ping = payload as DiagPingPayload;
        if (ping != null)
        {
            SendMessageToShell(LociMessageTypes.DiagPong, new DiagPongPayload { Nonce = ping.Nonce }, message.Id);
        }
    }

    public static void SendMessageToShell(string type, ILociMessagePayload payload, string replyTo = null)
    {
        if (!LociMessageTypes.IsUnityToShell(type))
        {
            Debug.LogWarning("[LociBridge] Refused to send an unknown or shell-to-Unity message type: " + type);
            return;
        }

        var expectedPayloadType = LociMessageModelRegistry.PayloadTypeFor(type);
        if (payload == null || expectedPayloadType == null || payload.GetType() != expectedPayloadType)
        {
            Debug.LogWarning("[LociBridge] Refused a payload type that does not match " + type + ".");
            return;
        }

        if (!payload.IsValid(out var payloadError))
        {
            Debug.LogWarning("[LociBridge] Refused invalid payload for " + type + ": " + payloadError);
            return;
        }

        if (LociMessageTypes.RequiresReplyTo(type) && string.IsNullOrEmpty(replyTo))
        {
            Debug.LogWarning("[LociBridge] Refused " + type + " because replyTo is required.");
            return;
        }
        if (replyTo != null && replyTo.Length < 1)
        {
            Debug.LogWarning("[LociBridge] Refused an empty replyTo value.");
            return;
        }

        var message = new JObject
        {
            ["v"] = 1,
            ["id"] = "u-" + Guid.NewGuid().ToString("N"),
            ["type"] = type,
            ["payload"] = JToken.FromObject(payload, JsonSerializer.Create(OutgoingSerializerSettings))
        };
        if (replyTo != null)
            message["replyTo"] = replyTo;

        var json = message.ToString(Formatting.None);
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

    private static void LogUnknownFields(JObject value, Type modelType, string context)
    {
        var contract = JsonSerializer.Create(IncomingSerializerSettings).ContractResolver.ResolveContract(modelType) as JsonObjectContract;
        if (contract == null)
            return;

        var knownNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in contract.Properties)
        {
            if (!property.Ignored && property.PropertyName != null)
                knownNames.Add(property.PropertyName);
        }

        foreach (var property in value.Properties())
        {
            if (!knownNames.Contains(property.Name))
                Debug.LogWarning("[LociBridge] Ignored unknown field '" + property.Name + "' in " + context + ".");
        }
    }
}
