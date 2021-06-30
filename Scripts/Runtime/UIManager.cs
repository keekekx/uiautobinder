using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class UIManager : MonoBehaviour
{
    public GameObject LayerTemplate;
    public static UIManager Instance;

    private Dictionary<string, UIContext> _uiDic = new Dictionary<string, UIContext>();
    private Dictionary<int, UILayer> _layerRootDic = new Dictionary<int, UILayer>();

    private Dictionary<UIContext, IUIUpdate> _uiUpdatesDic = new Dictionary<UIContext, IUIUpdate>();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private UILayer GetLayer(int layer)
    {
        if (!_layerRootDic.TryGetValue(layer, out var l))
        {
            var layerGO = Instantiate(LayerTemplate, transform, false);
            layerGO.name = $"layer_{layer}";
            layerGO.transform.SetParent(transform);
            l = layerGO.GetComponent<UILayer>();
            _layerRootDic.Add(layer, l);
        }

        return l;
    }

    public T Open<T>(params object[] data) where T : UIBase
    {
        var cfg = typeof(T).GetCustomAttribute<UIConfig>();
        if (cfg == null)
        {
            throw new Exception("UI需要配置UIConfig，具体详情查看UIConfig Attribute。");
        }

        var layer = GetLayer(cfg.Layer);
        if (!_uiDic.TryGetValue(cfg.Prefab, out var ctx))
        {
            ctx = new UIContext
            {
                Prefab = cfg.Prefab,
                Layer = layer,
                Params = data,
                State = State.Init,
            };

        }
        layer.OperatorOpen(ctx);

        if (ctx.UI is IUIUpdate updater && !_uiUpdatesDic.ContainsKey(ctx))
        {
            _uiUpdatesDic.Add(ctx, updater);
        }
        _uiDic[cfg.Prefab] = ctx;
        return ctx.UI as T;
    }

    public T GetUI<T>(string key) where T : UIBase
    {
        return _uiDic.TryGetValue(key, out var ctx) ? ctx.UI as T: default;
    }

    public void Close(string key)
    {
        if (!_uiDic.TryGetValue(key, out var ctx)) return;
        if (ctx.Layer.OperatorClose(ctx))
        {
            _uiUpdatesDic.Remove(ctx);
            Addressables.ReleaseInstance(ctx.UI.gameObject);
            _uiDic.Remove(key);
        }
    }

    public void Back(string key)
    {
        if (!_uiDic.TryGetValue(key, out var ctx)) return;
        if (ctx.Layer.Back(ctx))
        {
            _uiUpdatesDic.Remove(ctx);
            Addressables.ReleaseInstance(ctx.UI.gameObject);
            _uiDic.Remove(key);
        }
    }

    private void Update()
    {
        var delta = Time.deltaTime;
        foreach (var ups in _uiUpdatesDic)
        {
            if (ups.Key.State == State.Showing)
            {
                try
                {
                    ups.Value.OnUpdate(delta);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }
    }
}
