
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class EmotionCortexReceiver : MonoBehaviour
{
    [Header("Cortex app credentials (local development only; do not commit)")]
    public string clientId = "";
    public string clientSecret = "";
    [Header("Unity")]
    public EmotionSimulator simulator;
    public string serverUrl = "wss://localhost:6868";
    public bool connectOnStart = true;

    private ClientWebSocket socket;
    private CancellationTokenSource lifetime;
    private Task receiveTask;
    private readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();
    private readonly Dictionary<int, TaskCompletionSource<JObject>> pending =
        new Dictionary<int, TaskCompletionSource<JObject>>();
    private readonly object pendingLock = new object();
    private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
    private int nextId = 0;
    private string cortexToken;
    private string sessionId;
    private List<string> metricColumns = new List<string>();
    private volatile bool closing;
    private bool connected;
    private float lastSampleTime = -1000f;
    public string CurrentStatus { get; private set; } = "Not connected";

    async void Start()
    {
        if (simulator == null) simulator = GetComponent<EmotionSimulator>();
        if (simulator == null)
        {
            Debug.LogError("Cortex: assign EmotionSimulator in Inspector.");
            enabled = false;
            return;
        }
        simulator.automaticSimulation = false;
        if (connectOnStart) await ConnectAndSubscribe();
    }

    public async Task ConnectAndSubscribe()
    {
        if (connected || closing) return;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            SetStatus("Enter Cortex Client ID / Client Secret");
            return;
        }
        lifetime = new CancellationTokenSource();
        socket = new ClientWebSocket();
        try
        {
            SetStatus("Connecting to EMOTIV Cortex...");
            await socket.ConnectAsync(new Uri(serverUrl), lifetime.Token);
            connected = true;
            receiveTask = ReceiveLoop(lifetime.Token);
            SetStatus("Cortex WebSocket connected");

            JObject access = await Rpc("requestAccess", new JObject {
                ["clientId"] = clientId, ["clientSecret"] = clientSecret
            });
            if (access["result"]?["accessGranted"]?.Value<bool>() != true)
            {
                SetStatus("Approve application in EMOTIV Launcher and restart Play");
                return;
            }

            JObject auth = await Rpc("authorize", new JObject {
                ["clientId"] = clientId, ["clientSecret"] = clientSecret
            });
            cortexToken = auth["result"]?["cortexToken"]?.Value<string>();
            if (string.IsNullOrEmpty(cortexToken)) throw new Exception("No cortexToken in authorize response");

            JArray headsets = await QueryHeadsets();
            if (headsets.Count == 0)
            {
                SetStatus("No headsets: create and connect Virtual Brainwear in Launcher");
                return;
            }
            JObject chosen = null;
            foreach (JToken h in headsets)
                if (h["status"]?.Value<string>() == "connected") { chosen = h as JObject; break; }
            if (chosen == null)
            {
                string firstId = headsets[0]?["id"]?.Value<string>() ?? "unknown";
                SetStatus("Headset " + firstId + " not connected. Connect it in Launcher, restart Play");
                return;
            }
            string headsetId = chosen["id"]?.Value<string>();
            JObject session = await Rpc("createSession", new JObject {
                ["cortexToken"] = cortexToken,
                ["headset"] = headsetId,
                ["status"] = "open" // no premium session activation required
            });
            sessionId = session["result"]?["id"]?.Value<string>();
            if (string.IsNullOrEmpty(sessionId)) throw new Exception("No session ID");

            JObject sub = await Rpc("subscribe", new JObject {
                ["cortexToken"] = cortexToken,
                ["session"] = sessionId,
                ["streams"] = new JArray("met")
            });
            JArray successes = sub["result"]?["success"] as JArray;
            if (successes == null || successes.Count == 0)
                throw new Exception("'met' subscription unavailable: " +
                    (sub["result"]?["failure"]?.ToString() ?? sub.ToString()));

            metricColumns.Clear();
            foreach (JToken col in (JArray)successes[0]["cols"])
                metricColumns.Add(col.Value<string>());
            if (!metricColumns.Contains("str") || !metricColumns.Contains("eng") || !metricColumns.Contains("rel"))
                SetStatus("met subscribed but expected str/eng/rel not in columns: " + string.Join(",", metricColumns));
            else
                SetStatus("CORTEX MET SUBSCRIBED: waiting for samples (~10s on Basic)");
        }
        catch (Exception ex)
        {
            SetStatus("Cortex error: " + ex.Message);
        }
    }

    private async Task<JArray> QueryHeadsets()
    {
        JObject response = await Rpc("queryHeadsets", null);
        return response["result"] as JArray ?? new JArray();
    }

    private async Task<JObject> Rpc(string method, JObject parameters)
    {
        int id = Interlocked.Increment(ref nextId);
        JObject request = new JObject {
            ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method
        };
        if (parameters != null) request["params"] = parameters;
        var completion = new TaskCompletionSource<JObject>();
        lock (pendingLock) pending[id] = completion;
        await Send(request.ToString(Newtonsoft.Json.Formatting.None));
        Task finished = await Task.WhenAny(completion.Task, Task.Delay(15000, lifetime.Token));
        if (finished != completion.Task)
        {
            lock (pendingLock) pending.Remove(id);
            throw new TimeoutException(method + " timed out");
        }
        JObject result = await completion.Task;
        if (result["error"] != null)
            throw new Exception(method + " error: " + result["error"].ToString());
        return result;
    }

    private async Task Send(string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await sendLock.WaitAsync(lifetime.Token);
        try
        {
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text,
                true, lifetime.Token);
        }
        finally { sendLock.Release(); }
    }

    private async Task ReceiveLoop(CancellationToken token)
    {
        byte[] buffer = new byte[8192];
        try
        {
            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using (var stream = new MemoryStream())
                {
                    WebSocketReceiveResult part;
                    do
                    {
                        part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (part.MessageType == WebSocketMessageType.Close) return;
                        stream.Write(buffer, 0, part.Count);
                    } while (!part.EndOfMessage);
                    if (part.MessageType != WebSocketMessageType.Text) continue;
                    JObject msg;
                    try { msg = JObject.Parse(Encoding.UTF8.GetString(stream.ToArray())); }
                    catch { continue; }
                    if (msg["id"] != null && msg["id"].Type == JTokenType.Integer)
                    {
                        int id = msg["id"].Value<int>();
                        TaskCompletionSource<JObject> completion = null;
                        lock (pendingLock)
                        {
                            if (pending.TryGetValue(id, out completion)) pending.Remove(id);
                        }
                        if (completion != null) completion.TrySetResult(msg);
                    }
                    if (msg["met"] is JArray metricSample)
                        HandleMetrics(metricSample);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!closing) SetStatus("Cortex receive error: " + ex.Message);
        }
        finally
        {
            if (!closing) SetStatus("Cortex stream stopped");
        }
    }

    private void HandleMetrics(JArray sample)
    {
        if (metricColumns.Count == 0) return;
        float stress, engagement, relaxation;
        if (!ReadMetric(sample, "str", out stress) ||
            !ReadMetric(sample, "eng", out engagement) ||
            !ReadMetric(sample, "rel", out relaxation)) return;
        mainThread.Enqueue(() => {
            if (simulator == null) return;
            simulator.SetMetrics(stress, engagement, relaxation);
            lastSampleTime = Time.time;
            CurrentStatus = "Cortex met: live samples received";
        });
    }

    private bool ReadMetric(JArray sample, string column, out float value)
    {
        value = 0f;
        int index = metricColumns.IndexOf(column);
        if (index < 0 || index >= sample.Count) return false;
        int activeIndex = metricColumns.IndexOf(column + ".isActive");
        if (activeIndex >= 0 && activeIndex < sample.Count &&
            sample[activeIndex].Type == JTokenType.Boolean && !sample[activeIndex].Value<bool>()) return false;
        JToken number = sample[index];
        if (number == null || number.Type == JTokenType.Null) return false;
        try
        {
            float raw = number.Value<float>();
            if (float.IsNaN(raw) || float.IsInfinity(raw)) return false;
            value = Mathf.Clamp01(raw);
            return true;
        }
        catch { return false; }
    }

    private void Update()
    {
        Action action;
        while (mainThread.TryDequeue(out action)) action();
        if (lastSampleTime > 0 && Time.time - lastSampleTime > 35f &&
            CurrentStatus == "Cortex met: live samples received")
            CurrentStatus = "Waiting for next valid met sample";
    }

    private void SetStatus(string status)
    {
        mainThread.Enqueue(() => {
            CurrentStatus = status;
            Debug.Log("[EMOTIV Cortex] " + status);
        });
    }

    private void OnDestroy()
    {
        closing = true;
        if (lifetime != null) lifetime.Cancel();
        if (socket != null) socket.Dispose();
        if (lifetime != null) lifetime.Dispose();
    }
}
