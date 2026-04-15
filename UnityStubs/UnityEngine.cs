// =============================================================================
//  UnityEngine.cs  —  KotOR-Unity CLI build stubs
//  Mimics the Unity 2022 LTS API surface for dotnet build validation.
//  NOT used at runtime — Unity's actual assemblies replace this at play time.
// =============================================================================
#pragma warning disable CS0067, CS0649, CS0169, CS0414, CS1998, CS8019

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

// ── TMPro stub namespace (TextMeshPro) ────────────────────────────────────────
namespace TMPro
{
    public class TMP_Text : UnityEngine.MonoBehaviour
    {
        public string text { get; set; }
        public float  fontSize { get; set; }
        public UnityEngine.Color color { get; set; }
        public bool   raycastTarget { get; set; }
        public bool   richText { get; set; }
        public TextAlignmentOptions alignment { get; set; }
        public TextAlignmentOptions textAlignment { get; set; }
        public int    maxVisibleCharacters { get; set; }
        public float  characterSpacing { get; set; }
        public float  wordSpacing     { get; set; }
        public float  lineSpacing     { get; set; }
        public bool   overflowMode_mask { get; set; }
        public bool   enableWordWrapping { get; set; }
        public void   ForceMeshUpdate() { }
    }
    public enum TextAlignmentOptions
    {
        TopLeft = 257, Top = 258, TopRight = 260, TopJustified = 264, TopFlush = 272, TopGeoAligned = 288,
        Left = 513, Center = 514, Right = 516, Justified = 520, Flush = 528, CenterGeoAligned = 544,
        BottomLeft = 1025, Bottom = 1026, BottomRight = 1028, BottomJustified = 1032, BottomFlush = 1040, BottomGeoAligned = 1056,
        BaselineLeft = 2049, Baseline = 2050, BaselineRight = 2052, BaselineJustified = 2056, BaselineFlush = 2064, BaselineGeoAligned = 2080,
        MidlineLeft = 4097, Midline = 4098, MidlineRight = 4100, MidlineJustified = 4104, MidlineFlush = 4112, MidlineGeoAligned = 4128,
        CaplineLeft = 8193, Capline = 8194, CaplineRight = 8196, CaplineJustified = 8200, CaplineFlush = 8208, CaplineGeoAligned = 8224,
    }
    public class TextMeshProUGUI : TMP_Text { }
    public class TextMeshPro     : TMP_Text { }
    public class TMP_InputField  : UnityEngine.MonoBehaviour
    {
        public string text { get; set; }
        public UnityEngine.UI.InputField.ContentType contentType { get; set; }
        public class SubmitEvent : UnityEngine.Events.UnityEvent<string> { }
        public class OnChangeEvent : UnityEngine.Events.UnityEvent<string> { }
        public SubmitEvent onEndEdit   = new SubmitEvent();
        public OnChangeEvent onValueChanged = new OnChangeEvent();
        public void ActivateInputField() { }
        public void DeactivateInputField() { }
    }
    public class TMP_Dropdown : UnityEngine.MonoBehaviour
    {
        public int value { get; set; }
        public List<OptionData> options { get; set; } = new List<OptionData>();
        public class DropdownEvent : UnityEngine.Events.UnityEvent<int> { }
        public DropdownEvent onValueChanged = new DropdownEvent();
        public void AddOptions(List<string> opts) { }
        public void AddOptions(List<OptionData> opts) { }
        public void ClearOptions() { }
        public class OptionData { public string text; public OptionData() { } public OptionData(string t) { text = t; } }
    }
    public class TMP_FontAsset : UnityEngine.ScriptableObject { }
}

// ── Newtonsoft JSON stub ──────────────────────────────────────────────────────
namespace Newtonsoft.Json
{
    public static class JsonConvert
    {
        public static string SerializeObject(object o, Formatting fmt = Formatting.None) => "";
        public static T DeserializeObject<T>(string s) => default;
        public static object DeserializeObject(string s, Type t) => null;
    }
    public enum Formatting { None, Indented }
    public class JsonSerializerSettings { }
}


// =============================================================================
//  UnityEngine  —  core namespace
// =============================================================================
namespace UnityEngine
{
    // ── Enums ─────────────────────────────────────────────────────────────────
    public enum Space           { World, Self }
    public enum ForceMode       { Force, Impulse, VelocityChange, Acceleration }
    public enum ForceMode2D     { Force, Impulse }
    public enum SendMessageOptions { RequireReceiver, DontRequireReceiver }
    public enum HideFlags       { None=0, HideInHierarchy=1, HideInInspector=2, DontSaveInEditor=4, NotEditable=8, DontSaveInBuild=16, DontUnloadUnusedAsset=32 }
    public enum PrimitiveType   { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public enum KeyCode
    {
        None=0, Backspace=8, Tab=9, Return=13, Escape=27, Space=32, Delete=127,
        UpArrow=273, DownArrow=274, RightArrow=275, LeftArrow=276,
        F1=282, F2=283, F3=284, F4=285, F5=286, F6=287, F7=288, F8=289, F9=290, F10=291, F11=292, F12=293,
        Alpha0=48,Alpha1=49,Alpha2=50,Alpha3=51,Alpha4=52,Alpha5=53,Alpha6=54,Alpha7=55,Alpha8=56,Alpha9=57,
        A=97,B=98,C=99,D=100,E=101,F=102,G=103,H=104,I=105,J=106,K=107,L=108,M=109,
        N=110,O=111,P=112,Q=113,R=114,S=115,T=116,U=117,V=118,W=119,X=120,Y=121,Z=122,
        Mouse0=323, Mouse1=324, Mouse2=325, Mouse3=326, Mouse4=327, Mouse5=328, Mouse6=329,
        LeftShift=304, RightShift=303, LeftControl=306, RightControl=305, LeftAlt=308, RightAlt=307,
        LeftBracket=91, RightBracket=93, Semicolon=59, Quote=39, Comma=44, Period=46, Slash=47,
        Equals=61, Minus=45, Backquote=96, BackQuote=96,
        Keypad0=256, Keypad1=257, Keypad2=258, Keypad3=259, Keypad4=260,
        Keypad5=261, Keypad6=262, Keypad7=263, Keypad8=264, Keypad9=265,
        KeypadPeriod=266, KeypadDivide=267, KeypadMultiply=268, KeypadMinus=269,
        KeypadPlus=270, KeypadEnter=271, KeypadEquals=272,
        Insert=277, Home=278, End=279, PageUp=280, PageDown=281,
        Numlock=300, CapsLock=301, ScrollLock=302, Print=316, Pause=19
    }
    public enum TextAnchor      { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum TextAlignment   { Left, Center, Right, Justified }
    public enum FilterMode      { Point, Bilinear, Trilinear }
    public enum WrapMode        { Default, Once, Loop, PingPong, ClampForever }
    public enum AnimationBlendMode { Blend, Additive }
    public enum MixedLightingMode { IndirectOnly, Shadowmask, Subtractive }
    public enum LightType       { Spot, Directional, Point, Area, Disc }
    public enum LightShadows    { None, Hard, Soft }
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum RenderingPath   { UsePlayerSettings, VertexLit, Forward, DeferredLighting, DeferredShading }
    public enum CameraType      { Game, SceneView, Preview, VR, Reflection }
    public enum DepthTextureMode { None=0, Depth=1, DepthNormals=2, MotionVectors=4 }
    public enum ClearFlags         { Skybox=1, Color=2, SolidColor=2, Depth=3, Nothing=4 }
    public enum CameraClearFlags   { Skybox=1, Color=2, SolidColor=2, Depth=3, Nothing=4 }
    public enum CollisionDetectionMode { Discrete, Continuous, ContinuousDynamic, ContinuousSpeculative }
    public enum RigidbodyConstraints { None=0, FreezePositionX=2, FreezePositionY=4, FreezePositionZ=8, FreezeRotationX=16, FreezeRotationY=32, FreezeRotationZ=64, FreezePosition=14, FreezeRotation=112, FreezeAll=126 }
    public enum RigidbodyInterpolation { None, Interpolate, Extrapolate }
    public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }
    public enum AudioRolloffMode { Logarithmic, Linear, Custom }
    public enum AudioSpatialBlend { }
    public enum FFTWindow        { Rectangular, Triangle, Hamming, Hanning, Blackman, BlackmanHarris }
    public enum AudioVelocityUpdateMode { Auto, Fixed, Dynamic }
    public enum MeshTopology    { Triangles, Quads, Lines, LineStrip, Points }
    public enum IndexFormat     { UInt16, UInt32 }
    public enum NormalSolver    { Unweighted, AreaWeighted, AngleWeighted, BlendedWeighted }
    public enum RuntimePlatform { OSXEditor, OSXPlayer, WindowsPlayer, OSXWebPlayer, WebGLPlayer, WindowsWebPlayer, WindowsEditor, IPhonePlayer, Android, LinuxPlayer, LinuxEditor, PS4, XboxOne, WSAPlayerX86, WSAPlayerX64, WSAPlayerARM, Switch }
    public enum NetworkReachability { NotReachable, ReachableViaCarrierDataNetwork, ReachableViaLocalAreaNetwork }
    public enum SystemLanguage   { Afrikaans, Arabic, Basque, Belarusian, Bulgarian, Catalan, Chinese, ChineseSimplified, ChineseTraditional, Czech, Danish, Dutch, English, Estonian, Faroese, Finnish, French, German, Greek, Hebrew, Hugarian, Icelandic, Indonesian, Italian, Japanese, Korean, Latvian, Lithuanian, Norwegian, Polish, Portuguese, Romanian, Russian, SerboCroatian, Slovak, Slovenian, Spanish, Swedish, Thai, Turkish, Ukrainian, Vietnamese, Unknown }
    public enum FullScreenMode   { ExclusiveFullScreen, FullScreenWindow, MaximizedWindow, Windowed }
    public enum DeviceType       { Unknown, Handheld, Console, Desktop }

    // ── Structs ───────────────────────────────────────────────────────────────
    [Serializable] public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero    => new Vector2(0,0);
        public static Vector2 one     => new Vector2(1,1);
        public static Vector2 up      => new Vector2(0,1);
        public static Vector2 down    => new Vector2(0,-1);
        public static Vector2 left    => new Vector2(-1,0);
        public static Vector2 right   => new Vector2(1,0);
        public float magnitude        => (float)Math.Sqrt(x*x+y*y);
        public float sqrMagnitude     => x*x+y*y;
        public Vector2 normalized     { get { float m=magnitude; return m>0?new Vector2(x/m,y/m):zero; } }
        public static float Distance(Vector2 a, Vector2 b) => (a-b).magnitude;
        public static float Dot(Vector2 a, Vector2 b)      => a.x*b.x+a.y*b.y;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t=Math.Max(0,Math.Min(1,t)); return new Vector2(a.x+(b.x-a.x)*t, a.y+(b.y-a.y)*t); }
        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) => new Vector2(a.x+(b.x-a.x)*t, a.y+(b.y-a.y)*t);
        public static Vector2 MoveTowards(Vector2 c, Vector2 t, float d) { var diff=t-c; float m=diff.magnitude; return m<=d||m==0?t:c+diff/m*d; }
        public static Vector2 operator+(Vector2 a, Vector2 b) => new Vector2(a.x+b.x, a.y+b.y);
        public static Vector2 operator-(Vector2 a, Vector2 b) => new Vector2(a.x-b.x, a.y-b.y);
        public static Vector2 operator*(Vector2 a, float s)   => new Vector2(a.x*s, a.y*s);
        public static Vector2 operator*(float s, Vector2 a)   => new Vector2(a.x*s, a.y*s);
        public static Vector2 operator/(Vector2 a, float s)   => new Vector2(a.x/s, a.y/s);
        public static Vector2 operator-(Vector2 a)            => new Vector2(-a.x,-a.y);
        public static bool operator==(Vector2 a, Vector2 b)   => a.x==b.x&&a.y==b.y;
        public static bool operator!=(Vector2 a, Vector2 b)   => !(a==b);
        public static implicit operator Vector2(Vector3 v)    => new Vector2(v.x, v.y);
        public static implicit operator Vector3(Vector2 v)    => new Vector3(v.x, v.y, 0);
        public override bool Equals(object o) => o is Vector2 v && this==v;
        public override int GetHashCode() => x.GetHashCode()^y.GetHashCode();
        public override string ToString() => $"({x:F2},{y:F2})";
    }

    [Serializable] public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x=x; this.y=y; this.z=z; }
        public Vector3(float x, float y)          { this.x=x; this.y=y; this.z=0; }
        public static Vector3 zero    => new Vector3(0,0,0);
        public static Vector3 one     => new Vector3(1,1,1);
        public static Vector3 forward => new Vector3(0,0,1);
        public static Vector3 back    => new Vector3(0,0,-1);
        public static Vector3 up      => new Vector3(0,1,0);
        public static Vector3 down    => new Vector3(0,-1,0);
        public static Vector3 right   => new Vector3(1,0,0);
        public static Vector3 left    => new Vector3(-1,0,0);
        public static Vector3 positiveInfinity => new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        public static Vector3 negativeInfinity => new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        public float magnitude    => (float)Math.Sqrt(x*x+y*y+z*z);
        public float sqrMagnitude => x*x+y*y+z*z;
        public Vector3 normalized { get { float m=magnitude; return m>0?new Vector3(x/m,y/m,z/m):zero; } }
        public static float Distance(Vector3 a, Vector3 b) => (a-b).magnitude;
        public static float Dot(Vector3 a, Vector3 b)      => a.x*b.x+a.y*b.y+a.z*b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b)  => new Vector3(a.y*b.z-a.z*b.y, a.z*b.x-a.x*b.z, a.x*b.y-a.y*b.x);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t=Math.Max(0,Math.Min(1,t)); return new Vector3(a.x+(b.x-a.x)*t, a.y+(b.y-a.y)*t, a.z+(b.z-a.z)*t); }
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => new Vector3(a.x+(b.x-a.x)*t, a.y+(b.y-a.y)*t, a.z+(b.z-a.z)*t);
        public static Vector3 MoveTowards(Vector3 c, Vector3 t, float d) { var diff=t-c; float m=diff.magnitude; return m<=d||m==0?t:c+diff/m*d; }
        public static Vector3 Reflect(Vector3 v, Vector3 n) => v - 2*Dot(v,n)*n;
        public static Vector3 Project(Vector3 v, Vector3 n) { float d=Dot(n,n); return d<1e-10f?zero:n*(Dot(v,n)/d); }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) => v - Project(v,n);
        public static float Angle(Vector3 a, Vector3 b) { float d=Dot(a.normalized,b.normalized); return (float)(Math.Acos(Math.Max(-1,Math.Min(1,d)))*180/Math.PI); }
        public static float SignedAngle(Vector3 a, Vector3 b, Vector3 axis) => Angle(a,b);
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 velocity, float smoothTime) { velocity=Vector3.zero; return target; }
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 velocity, float smoothTime, float maxSpeed, float dt) { velocity=Vector3.zero; return target; }
        public static Vector3 RotateTowards(Vector3 c, Vector3 t, float maxRad, float maxMag) => t.normalized;
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x*b.x, a.y*b.y, a.z*b.z);
        public static Vector3 Normalize(Vector3 v) => v.normalized;
        public void Normalize() { float m = magnitude; if (m > 0) { x /= m; y /= m; z /= m; } }
        public static Vector3 operator+(Vector3 a, Vector3 b) => new Vector3(a.x+b.x, a.y+b.y, a.z+b.z);
        public static Vector3 operator-(Vector3 a, Vector3 b) => new Vector3(a.x-b.x, a.y-b.y, a.z-b.z);
        public static Vector3 operator*(Vector3 a, float s)   => new Vector3(a.x*s, a.y*s, a.z*s);
        public static Vector3 operator*(float s, Vector3 a)   => new Vector3(a.x*s, a.y*s, a.z*s);
        public static Vector3 operator/(Vector3 a, float s)   => new Vector3(a.x/s, a.y/s, a.z/s);
        public static Vector3 operator/(Vector3 a, int s)     => new Vector3(a.x/s, a.y/s, a.z/s);
        public static Vector3 operator-(Vector3 a)            => new Vector3(-a.x,-a.y,-a.z);
        public static bool operator==(Vector3 a, Vector3 b)   => a.x==b.x&&a.y==b.y&&a.z==b.z;
        public static bool operator!=(Vector3 a, Vector3 b)   => !(a==b);
        public override bool Equals(object o) => o is Vector3 v && this==v;
        public override int GetHashCode() => x.GetHashCode()^y.GetHashCode()^z.GetHashCode();
        public override string ToString() => $"({x:F2},{y:F2},{z:F2})";
    }

    [Serializable] public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x=x; this.y=y; this.z=z; this.w=w; }
        public static Vector4 zero => new Vector4(0,0,0,0);
        public static Vector4 one  => new Vector4(1,1,1,1);
        public float magnitude     => (float)Math.Sqrt(x*x+y*y+z*z+w*w);
        public Vector4 normalized  { get { float m=magnitude; return m>0?new Vector4(x/m,y/m,z/m,w/m):zero; } }
        public static Vector4 Lerp(Vector4 a, Vector4 b, float t) { t=Math.Max(0,Math.Min(1,t)); return new Vector4(a.x+(b.x-a.x)*t, a.y+(b.y-a.y)*t, a.z+(b.z-a.z)*t, a.w+(b.w-a.w)*t); }
        public static Vector4 operator+(Vector4 a, Vector4 b) => new Vector4(a.x+b.x,a.y+b.y,a.z+b.z,a.w+b.w);
        public static Vector4 operator-(Vector4 a, Vector4 b) => new Vector4(a.x-b.x,a.y-b.y,a.z-b.z,a.w-b.w);
        public static Vector4 operator*(Vector4 a, float s)   => new Vector4(a.x*s,a.y*s,a.z*s,a.w*s);
        public static implicit operator Vector4(Vector3 v)    => new Vector4(v.x,v.y,v.z,0);
        public static implicit operator Vector3(Vector4 v)    => new Vector3(v.x,v.y,v.z);
    }

    [Serializable] public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x=x; this.y=y; this.z=z; this.w=w; }
        public static Quaternion identity => new Quaternion(0,0,0,1);
        public Vector3 eulerAngles { get => Vector3.zero; set { } }
        public static Quaternion Euler(float x, float y, float z) => identity;
        public static Quaternion Euler(Vector3 e) => identity;
        public static Quaternion AngleAxis(float angle, Vector3 axis) => identity;
        public static Quaternion LookRotation(Vector3 forward) => identity;
        public static Quaternion LookRotation(Vector3 forward, Vector3 up) => identity;
        public static Quaternion FromToRotation(Vector3 from, Vector3 to) => identity;
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) => identity;
        public static Quaternion LerpUnclamped(Quaternion a, Quaternion b, float t) => identity;
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => identity;
        public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t) => identity;
        public static Quaternion RotateTowards(Quaternion f, Quaternion t, float step) => t;
        public static Quaternion Inverse(Quaternion q) => q;
        public static float Angle(Quaternion a, Quaternion b) => 0f;
        public static Quaternion Normalize(Quaternion q) => q;
        public static Quaternion operator*(Quaternion a, Quaternion b) => identity;
        public static Vector3    operator*(Quaternion q, Vector3 v)    => v;
        public static bool operator==(Quaternion a, Quaternion b) => a.x==b.x&&a.y==b.y&&a.z==b.z&&a.w==b.w;
        public static bool operator!=(Quaternion a, Quaternion b) => !(a==b);
        public override bool Equals(object o) => o is Quaternion q && this==q;
        public override int GetHashCode() => x.GetHashCode()^y.GetHashCode()^z.GetHashCode()^w.GetHashCode();
    }

    [Serializable] public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a=1) { this.r=r; this.g=g; this.b=b; this.a=a; }
        public static Color white   => new Color(1,1,1,1);
        public static Color black   => new Color(0,0,0,1);
        public static Color red     => new Color(1,0,0,1);
        public static Color green   => new Color(0,1,0,1);
        public static Color blue    => new Color(0,0,1,1);
        public static Color yellow  => new Color(1,0.92f,0.016f,1);
        public static Color cyan    => new Color(0,1,1,1);
        public static Color magenta => new Color(1,0,1,1);
        public static Color gray    => new Color(0.5f,0.5f,0.5f,1);
        public static Color grey    => new Color(0.5f,0.5f,0.5f,1);
        public static Color clear   => new Color(0,0,0,0);
        public static Color Lerp(Color a, Color b, float t) { t=Math.Max(0,Math.Min(1,t)); return new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t); }
        public static Color LerpUnclamped(Color a, Color b, float t) => new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);
        public static Color operator*(Color a, Color b) => new Color(a.r*b.r,a.g*b.g,a.b*b.b,a.a*b.a);
        public static Color operator*(Color a, float s) => new Color(a.r*s,a.g*s,a.b*s,a.a*s);
        public static Color operator+(Color a, Color b) => new Color(a.r+b.r,a.g+b.g,a.b+b.b,a.a+b.a);
        public static bool operator==(Color a, Color b) => a.r==b.r&&a.g==b.g&&a.b==b.b&&a.a==b.a;
        public static bool operator!=(Color a, Color b) => !(a==b);
        public override bool Equals(object o) => o is Color c && this==c;
        public override int GetHashCode() => r.GetHashCode()^g.GetHashCode()^b.GetHashCode()^a.GetHashCode();
        public static Color HSVToRGB(float h, float s, float v) => new Color(v,v,v,1);
        public static void RGBToHSV(Color c, out float h, out float s, out float v) { h=s=0; v=c.r; }
    }

    [Serializable] public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r=r; this.g=g; this.b=b; this.a=a; }
        public static implicit operator Color(Color32 c) => new Color(c.r/255f,c.g/255f,c.b/255f,c.a/255f);
        public static implicit operator Color32(Color c) => new Color32((byte)(c.r*255),(byte)(c.g*255),(byte)(c.b*255),(byte)(c.a*255));
    }

    [Serializable] public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x=x; this.y=y; this.width=w; this.height=h; }
        public float xMin { get=>x; set=>x=value; }
        public float yMin { get=>y; set=>y=value; }
        public float xMax { get=>x+width; set=>width=value-x; }
        public float yMax { get=>y+height; set=>height=value-y; }
        public Vector2 position { get=>new Vector2(x,y); set{x=value.x;y=value.y;} }
        public Vector2 size     { get=>new Vector2(width,height); set{width=value.x;height=value.y;} }
        public Vector2 center   => new Vector2(x+width/2,y+height/2);
        public bool Contains(Vector2 p) => p.x>=x&&p.x<=x+width&&p.y>=y&&p.y<=y+height;
        public bool Contains(Vector3 p) => p.x>=x&&p.x<=x+width&&p.y>=y&&p.y<=y+height;
        public bool Overlaps(Rect o) => x<o.xMax&&xMax>o.x&&y<o.yMax&&yMax>o.y;
        public static Rect zero => new Rect(0,0,0,0);
    }

    [Serializable] public struct Bounds
    {
        public Vector3 center, size;
        public Bounds(Vector3 center, Vector3 size) { this.center=center; this.size=size; }
        public Vector3 extents { get=>size/2; set=>size=value*2; }
        public Vector3 min     => center-extents;
        public Vector3 max     => center+extents;
        public bool Contains(Vector3 p) => p.x>=min.x&&p.x<=max.x&&p.y>=min.y&&p.y<=max.y&&p.z>=min.z&&p.z<=max.z;
        public bool Intersects(Bounds b) => min.x<=b.max.x&&max.x>=b.min.x&&min.y<=b.max.y&&max.y>=b.min.y&&min.z<=b.max.z&&max.z>=b.min.z;
        public void Encapsulate(Vector3 p) { }
        public void Encapsulate(Bounds b) { }
        public void Expand(float a) { }
    }

    [Serializable] public struct Matrix4x4
    {
        public float m00,m01,m02,m03,m10,m11,m12,m13,m20,m21,m22,m23,m30,m31,m32,m33;
        public static Matrix4x4 identity { get { var m=new Matrix4x4(); m.m00=m.m11=m.m22=m.m33=1; return m; } }
        public static Matrix4x4 zero     => new Matrix4x4();
        public Vector4 GetColumn(int i) => Vector4.zero;
        public Vector4 GetRow(int i)    => Vector4.zero;
        public void SetColumn(int i, Vector4 v) { }
        public void SetRow(int i, Vector4 v) { }
        public Vector3 MultiplyPoint(Vector3 p) => p;
        public Vector3 MultiplyPoint3x4(Vector3 p) => p;
        public Vector3 MultiplyVector(Vector3 v) => v;
        public static Matrix4x4 TRS(Vector3 pos, Quaternion rot, Vector3 scale) => identity;
        public static Matrix4x4 LookAt(Vector3 from, Vector3 to, Vector3 up) => identity;
        public static Matrix4x4 Perspective(float fov, float aspect, float near, float far) => identity;
        public static Matrix4x4 Ortho(float l, float r, float b, float t, float near, float far) => identity;
        public static Matrix4x4 Translate(Vector3 v) => identity;
        public static Matrix4x4 Rotate(Quaternion q) => identity;
        public static Matrix4x4 Scale(Vector3 v) => identity;
        public Matrix4x4 inverse => identity;
        public Matrix4x4 transpose => identity;
        public static Matrix4x4 operator*(Matrix4x4 a, Matrix4x4 b) => identity;
        public static Vector4    operator*(Matrix4x4 m, Vector4 v)   => v;
        public static bool operator==(Matrix4x4 a, Matrix4x4 b) => true;
        public static bool operator!=(Matrix4x4 a, Matrix4x4 b) => false;
        public override bool Equals(object o) => true;
        public override int GetHashCode() => 0;
        public float this[int row, int col] { get=>0; set{} }
        public float this[int i] { get=>0; set{} }
        public Quaternion rotation => Quaternion.identity;
        public Vector3    lossyScale => Vector3.one;
        public bool isIdentity => true;
        public float determinant => 1f;
        public bool ValidTRS() => true;
        public static Matrix4x4 Frustum(float l, float r, float b, float t, float near, float far) => identity;
    }

    public struct BoneWeight
    {
        public float weight0, weight1, weight2, weight3;
        public int   boneIndex0, boneIndex1, boneIndex2, boneIndex3;
    }

    public struct RefreshRate { public double value { get; private set; } public RefreshRate(double v) { value = v; } }
    [Serializable] public struct Resolution
    {
        public int width, height, refreshRate;
        public RefreshRate refreshRateRatio;
        public override string ToString() => $"{width}x{height}@{refreshRate}";
    }

    public struct RaycastHit
    {
        public Vector3    point, normal;
        public float      distance;
        public Collider   collider;
        public Transform  transform;
        public Rigidbody  rigidbody;
        public Vector2    textureCoord;
        public Vector2    textureCoord2;
        public int        triangleIndex;
        public Vector3    barycentricCoordinate;
    }

    public struct Ray
    {
        public Vector3 origin, direction;
        public Ray(Vector3 o, Vector3 d) { origin=o; direction=d; }
        public Vector3 GetPoint(float d) => origin+direction*d;
    }

    public struct Plane
    {
        public Vector3 normal; public float distance;
        public Plane(Vector3 n, float d) { normal=n; distance=d; }
        public Plane(Vector3 n, Vector3 p) { normal=n; distance=-Vector3.Dot(n,p); }
        public float GetDistanceToPoint(Vector3 p) => Vector3.Dot(normal,p)+distance;
        public bool GetSide(Vector3 p) => GetDistanceToPoint(p)>0;
        public bool Raycast(Ray r, out float d) { d=0; return false; }
    }

    public class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s) { } }
    public class WaitForSecondsRealtime : YieldInstruction { public WaitForSecondsRealtime(float s) { } }
    public class WaitForEndOfFrame : YieldInstruction { }
    public class WaitForFixedUpdate : YieldInstruction { }
    public class WaitUntil : YieldInstruction { public WaitUntil(Func<bool> p) { } }
    public class WaitWhile  : YieldInstruction { public WaitWhile(Func<bool> p) { } }
    public class YieldInstruction { }
    public class CustomYieldInstruction : YieldInstruction { public virtual bool keepWaiting => false; }
    public class AsyncOperation : YieldInstruction
    {
        public bool   isDone { get; protected set; }
        public float  progress { get; protected set; }
        public bool   allowSceneActivation { get; set; }
        public event Action<AsyncOperation> completed;
    }


    // ── Core Object Hierarchy ─────────────────────────────────────────────────
    public class Object
    {
        public string name { get; set; }
        public HideFlags hideFlags { get; set; }
        public int GetInstanceID() => GetHashCode();
        public override string ToString() => name ?? GetType().Name;
        public static implicit operator bool(Object o) => o != null;
        public static void Destroy(Object obj, float t = 0f) { }
        public static void DestroyImmediate(Object obj, bool allowDestroyingAssets = false) { }
        public static void DontDestroyOnLoad(Object obj) { }
        public static T FindObjectOfType<T>() where T : Object => null;
        public static Object FindObjectOfType(Type t) => null;
        public static T[] FindObjectsOfType<T>() where T : Object => new T[0];
        public static Object[] FindObjectsOfType(Type t) => new Object[0];
        public static T Instantiate<T>(T original) where T : Object => original;
        public static T Instantiate<T>(T original, Transform parent) where T : Object => original;
        public static T Instantiate<T>(T original, Vector3 pos, Quaternion rot) where T : Object => original;
        public static T Instantiate<T>(T original, Vector3 pos, Quaternion rot, Transform parent) where T : Object => original;
        public static Object Instantiate(Object original) => original;
        public static Object Instantiate(Object original, Transform parent) => original;
        public static Object Instantiate(Object original, Vector3 pos, Quaternion rot) => original;
        public static T Load<T>(string path) where T : Object => null;
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => Activator.CreateInstance<T>();
        public static ScriptableObject CreateInstance(string className) => null;
        public static ScriptableObject CreateInstance(Type type) => (ScriptableObject)Activator.CreateInstance(type);
    }

    public class Component : Object
    {
        public GameObject  gameObject  { get; }
        public Transform   transform   { get; }
        public string      tag         { get; set; }
        public bool        enabled     { get; set; }
        public T GetComponent<T>() where T : Component => null;
        public Component GetComponent(Type t) => null;
        public T GetComponentInChildren<T>(bool includeInactive = false) where T : Component => null;
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : Component => new T[0];
        public T GetComponentInParent<T>() where T : Component => null;
        public T[] GetComponentsInParent<T>(bool includeInactive = false) where T : Component => new T[0];
        public bool TryGetComponent<T>(out T component) where T : Component { component = null; return false; }
        public void SendMessage(string method, object value = null, SendMessageOptions opts = SendMessageOptions.RequireReceiver) { }
        public void SendMessageUpwards(string method, object value = null, SendMessageOptions opts = SendMessageOptions.RequireReceiver) { }
        public void BroadcastMessage(string method, object value = null, SendMessageOptions opts = SendMessageOptions.RequireReceiver) { }
        public bool CompareTag(string t) => tag == t;
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }
        public bool isActiveAndEnabled { get; }
    }

    public class MonoBehaviour : Behaviour
    {
        public bool useGUILayout { get; set; }
        public bool runInEditMode { get; set; }
        public Coroutine StartCoroutine(IEnumerator routine) => null;
        public Coroutine StartCoroutine(string methodName, object value = null) => null;
        public void StopCoroutine(Coroutine routine) { }
        public void StopCoroutine(string methodName) { }
        public void StopAllCoroutines() { }
        public void Invoke(string method, float time) { }
        public void InvokeRepeating(string method, float time, float repeat) { }
        public void CancelInvoke(string method = null) { }
        public bool IsInvoking(string method = null) => false;
        public static void print(object msg) { }
    }

    public class Coroutine : YieldInstruction { }

    public class Transform : Component, IEnumerable<Transform>
    {
        public Vector3    position         { get; set; }
        public Vector3    localPosition    { get; set; }
        public Quaternion rotation         { get; set; }
        public Quaternion localRotation    { get; set; }
        public Vector3    localScale       { get; set; }
        public Vector3    lossyScale       { get; }
        public Vector3    eulerAngles      { get; set; }
        public Vector3    localEulerAngles { get; set; }
        public Vector3    forward          { get; set; }
        public Vector3    right            { get; set; }
        public Vector3    up               { get; set; }
        public Transform  parent           { get; set; }
        public Transform  root             { get; }
        public int        childCount       { get; }
        public Matrix4x4  localToWorldMatrix { get; }
        public Matrix4x4  worldToLocalMatrix { get; }
        public bool       hasChanged       { get; set; }
        public int        hierarchyCapacity { get; set; }
        public int        hierarchyCount    { get; }
        public void Translate(Vector3 t, Space s = Space.Self) { }
        public void Translate(float x, float y, float z, Space s = Space.Self) { }
        public void Rotate(Vector3 eulers, Space s = Space.Self) { }
        public void Rotate(float x, float y, float z, Space s = Space.Self) { }
        public void Rotate(Vector3 axis, float angle, Space s = Space.Self) { }
        public void RotateAround(Vector3 point, Vector3 axis, float angle) { }
        public void LookAt(Transform t, Vector3 up = default) { }
        public void LookAt(Vector3 worldPos, Vector3 up = default) { }
        public Vector3 TransformPoint(Vector3 p) => p;
        public Vector3 TransformPoint(float x, float y, float z) => new Vector3(x,y,z);
        public Vector3 InverseTransformPoint(Vector3 p) => p;
        public Vector3 InverseTransformPoint(float x, float y, float z) => new Vector3(x,y,z);
        public Vector3 TransformDirection(Vector3 d) => d;
        public Vector3 InverseTransformDirection(Vector3 d) => d;
        public Vector3 TransformVector(Vector3 v) => v;
        public Vector3 InverseTransformVector(Vector3 v) => v;
        public Transform GetChild(int i) => null;
        public int GetSiblingIndex() => 0;
        public void SetSiblingIndex(int i) { }
        public void SetAsFirstSibling() { }
        public void SetAsLastSibling() { }
        public Transform Find(string name) => null;
        public bool IsChildOf(Transform t) => false;
        public void SetParent(Transform p, bool worldPositionStays = true) { }
        public void DetachChildren() { }
        public IEnumerator<Transform> GetEnumerator() => new List<Transform>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class RectTransform : Transform
    {
        public Vector2 anchoredPosition  { get; set; }
        public Vector2 sizeDelta         { get; set; }
        public Vector2 anchorMin         { get; set; }
        public Vector2 anchorMax         { get; set; }
        public Vector2 pivot             { get; set; }
        public Vector3 anchoredPosition3D { get; set; }
        public Rect    rect              { get; }
        public Vector2 offsetMin         { get; set; }
        public Vector2 offsetMax         { get; set; }
        public void ForceUpdateRectTransforms() { }
        public void GetLocalCorners(Vector3[] fourCornersArray) { }
        public void GetWorldCorners(Vector3[] fourCornersArray) { }
    }

    public class GameObject : Object
    {
        public Transform  transform      { get; }
        public bool       activeSelf     { get; }
        public bool       activeInHierarchy { get; }
        public string     tag            { get; set; }
        public int        layer          { get; set; }
        public Scene      scene          { get; }
        public bool       isStatic       { get; set; }
        public GameObject() { }
        public GameObject(string name) { this.name = name; }
        public GameObject(string name, params Type[] components) { this.name = name; }
        public void SetActive(bool v) { }
        public T AddComponent<T>() where T : Component => default;
        public Component AddComponent(Type t) => null;
        public T GetComponent<T>() where T : Component => null;
        public Component GetComponent(Type t) => null;
        public T GetComponent<T>(out T comp) where T : Component { comp = null; return null; }
        public bool TryGetComponent<T>(out T comp) where T : Component { comp = null; return false; }
        public T GetComponentInChildren<T>(bool inactive = false) where T : Component => null;
        public T[] GetComponentsInChildren<T>(bool inactive = false) where T : Component => new T[0];
        public T GetComponentInParent<T>() where T : Component => null;
        public bool CompareTag(string t) => tag == t;
        public void SendMessage(string method, object value = null, SendMessageOptions opts = SendMessageOptions.RequireReceiver) { }
        public void BroadcastMessage(string method, object value = null, SendMessageOptions opts = SendMessageOptions.RequireReceiver) { }
        public static GameObject Find(string name) => null;
        public static GameObject FindWithTag(string tag) => null;
        public static GameObject FindGameObjectWithTag(string tag) => null;
        public static GameObject[] FindGameObjectsWithTag(string tag) => new GameObject[0];
        public static GameObject CreatePrimitive(PrimitiveType type) => new GameObject(type.ToString());
    }

    public struct Scene
    {
        public string name { get; }
        public string path { get; }
        public int    buildIndex { get; }
        public bool   isLoaded  { get; }
        public bool   IsValid() => false;
        public List<GameObject> GetRootGameObjects() => new List<GameObject>();
        public void GetRootGameObjects(List<GameObject> rootGameObjects) { }
    }

    // ── Core static classes ───────────────────────────────────────────────────
    public static class Debug
    {
        public static void Log(object msg) { Console.WriteLine($"[UE:Log] {msg}"); }
        public static void Log(object msg, Object ctx) { Console.WriteLine($"[UE:Log] {msg}"); }
        public static void LogWarning(object msg) { Console.WriteLine($"[UE:Warn] {msg}"); }
        public static void LogWarning(object msg, Object ctx) { Console.WriteLine($"[UE:Warn] {msg}"); }
        public static void LogError(object msg) { Console.Error.WriteLine($"[UE:Error] {msg}"); }
        public static void LogError(object msg, Object ctx) { Console.Error.WriteLine($"[UE:Error] {msg}"); }
        public static void LogException(Exception e) { Console.Error.WriteLine($"[UE:Exception] {e}"); }
        public static void LogException(Exception e, Object ctx) { Console.Error.WriteLine($"[UE:Exception] {e}"); }
        public static void Assert(bool cond, string msg = "") { if (!cond) Console.Error.WriteLine($"[UE:Assert] {msg}"); }
        public static void DrawLine(Vector3 start, Vector3 end, Color color = default, float duration = 0, bool depthTest = true) { }
        public static void DrawRay(Vector3 start, Vector3 dir, Color color = default, float duration = 0, bool depthTest = true) { }
        public static void Break() { }
        public static bool isDebugBuild => true;
        public static bool developerConsoleVisible { get; set; }
    }

    public static class Application
    {
        public static string dataPath            { get; }
        public static string persistentDataPath  { get; }
        public static string streamingAssetsPath { get; }
        public static string temporaryCachePath  { get; }
        public static string productName         { get; }
        public static string companyName         { get; }
        public static string version             { get; }
        public static string unityVersion        { get; }
        public static bool   isPlaying           { get; }
        public static bool   isEditor            { get; }
        public static bool   isFocused           { get; }
        public static bool   isBatchMode         { get; }
        public static bool   runInBackground     { get; set; }
        public static bool   isMobilePlatform    { get; }
        public static bool   isConsolePlatform   { get; }
        public static RuntimePlatform platform   { get; }
        public static SystemLanguage systemLanguage { get; }
        public static NetworkReachability internetReachability { get; }
        public static int    targetFrameRate     { get; set; }
        public static float  backgroundLoadingPriority { get; set; }
        public static event Action<bool> focusChanged;
        public static event Action<string> logMessageReceived;
        public static event Action<string,string,LogType> logMessageReceivedThreaded;
        public static event Action quitting;
        public static void Quit(int exitCode = 0) { }
        public static void OpenURL(string url) { }
        public static bool CanStreamedLevelBeLoaded(int lvl) => false;
        public static bool CanStreamedLevelBeLoaded(string name) => false;
        public static string[] GetBuildTags() => new string[0];
        public static void CaptureScreenshot(string path, int supersize = 1) { }
    }

    public enum LogType { Error, Assert, Warning, Log, Exception }

    public static class Time
    {
        public static float time            { get; }
        public static float unscaledTime    { get; }
        public static float deltaTime       { get; }
        public static float unscaledDeltaTime { get; }
        public static float fixedDeltaTime  { get; set; }
        public static float fixedUnscaledDeltaTime { get; }
        public static float timeScale       { get; set; }
        public static float maximumDeltaTime { get; set; }
        public static float smoothDeltaTime { get; }
        public static float realTimeSinceStartup { get; }
        public static float realtimeSinceStartup { get; }  // lowercase alias
        public static float timeSinceLevelLoad { get; }
        public static int   frameCount     { get; }
        public static int   renderedFrameCount { get; }
        public static bool  inFixedTimeStep { get; }
    }

    public static class Input
    {
        public static Vector3 mousePosition   { get; }
        public static Vector2 mouseScrollDelta { get; }
        public static bool    anyKey          { get; }
        public static bool    anyKeyDown      { get; }
        public static string  inputString     { get; }
        public static bool    mousePresent    { get; }
        public static bool    touchSupported  { get; }
        public static int     touchCount      { get; }
        public static bool GetKey(KeyCode key)       => false;
        public static bool GetKey(string name)       => false;
        public static bool GetKeyDown(KeyCode key)   => false;
        public static bool GetKeyDown(string name)   => false;
        public static bool GetKeyUp(KeyCode key)     => false;
        public static bool GetKeyUp(string name)     => false;
        public static bool GetMouseButton(int btn)   => false;
        public static bool GetMouseButtonDown(int btn) => false;
        public static bool GetMouseButtonUp(int btn) => false;
        public static float GetAxis(string name)     => 0f;
        public static float GetAxisRaw(string name)  => 0f;
        public static bool GetButton(string name)    => false;
        public static bool GetButtonDown(string name) => false;
        public static bool GetButtonUp(string name)  => false;
        public static Touch GetTouch(int i) => default;
        public static bool IsJoystickPreconfigured(string name) => false;
        public static string[] GetJoystickNames() => new string[0];
        public static void ResetInputAxes() { }
        public static bool simulateMouseWithTouches { get; set; }
        public static bool compensateSensors { get; set; }
        public static Vector3 acceleration { get; }
    }

    public struct Touch
    {
        public int fingerId; public Vector2 position, deltaPosition, rawPosition;
        public float deltaTime; public int tapCount;
        public TouchPhase phase;
    }
    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }

    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public const float Rad2Deg = (float)(180.0 / Math.PI);
        public const float Epsilon = float.Epsilon;
        public static float Sin(float f)   => (float)Math.Sin(f);
        public static float Cos(float f)   => (float)Math.Cos(f);
        public static float Tan(float f)   => (float)Math.Tan(f);
        public static float Asin(float f)  => (float)Math.Asin(f);
        public static float Acos(float f)  => (float)Math.Acos(f);
        public static float Atan(float f)  => (float)Math.Atan(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Sqrt(float f)  => (float)Math.Sqrt(f);
        public static float Abs(float f)   => (float)Math.Abs(f);
        public static int   Abs(int i)     => Math.Abs(i);
        public static float Pow(float f, float p) => (float)Math.Pow(f, p);
        public static float Exp(float p)   => (float)Math.Exp(p);
        public static float Log(float f)   => (float)Math.Log(f);
        public static float Log(float f, float p) => (float)Math.Log(f, p);
        public static float Log10(float f) => (float)Math.Log10(f);
        public static float Floor(float f) => (float)Math.Floor(f);
        public static int   FloorToInt(float f) => (int)Math.Floor(f);
        public static float Ceil(float f)  => (float)Math.Ceiling(f);
        public static int   CeilToInt(float f) => (int)Math.Ceiling(f);
        public static float Round(float f) => (float)Math.Round(f);
        public static int   RoundToInt(float f) => (int)Math.Round(f);
        public static float Sign(float f)  => f >= 0 ? 1f : -1f;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Min(params float[] vals) { float m=float.MaxValue; foreach(var v in vals) if(v<m) m=v; return m; }
        public static int   Min(int a, int b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Max(params float[] vals) { float m=float.MinValue; foreach(var v in vals) if(v>m) m=v; return m; }
        public static int   Max(int a, int b) => a > b ? a : b;
        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
        public static int   Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float LerpAngle(float a, float b, float t) => Lerp(a, b, t);
        public static float SmoothStep(float from, float to, float t) { t=Clamp01(t); t=t*t*(3-2*t); return Lerp(from,to,t); }
        public static float SmoothDamp(float c, float t, ref float vel, float time, float maxSpeed = Infinity, float dt = 0) { vel=0; return t; }
        public static float MoveTowards(float c, float t, float d) { float diff=t-c; if(Abs(diff)<=d) return t; return c+Sign(diff)*d; }
        public static float MoveTowardsAngle(float c, float t, float d) => MoveTowards(c, t, d);
        public static float Repeat(float t, float len) => t - Floor(t / len) * len;
        public static float PingPong(float t, float len) { t = Repeat(t, len*2); return len - Abs(t - len); }
        public static float DeltaAngle(float c, float t) { float d=Repeat(t-c,360); return d>180?d-360:d; }
        public static float InverseLerp(float a, float b, float v) => Abs(b-a) < Epsilon ? 0f : Clamp01((v-a)/(b-a)); 
        public static bool Approximately(float a, float b) => Abs(b-a) < Max(1e-6f*Max(Abs(a),Abs(b)), Epsilon*8);
        public static bool IsPowerOfTwo(int v) => v>0 && (v&(v-1))==0;
        public static int  NextPowerOfTwo(int v) { v--; v|=v>>1; v|=v>>2; v|=v>>4; v|=v>>8; v|=v>>16; return v+1; }
        public static int  ClosestPowerOfTwo(int v) => NextPowerOfTwo(v);
        public static float GammaToLinearSpace(float v) => v;
        public static float LinearToGammaSpace(float v) => v;
        public static float PerlinNoise(float x, float y) => 0.5f;
    }

    public static class Random
    {
        private static readonly System.Random _r = new System.Random();
        public static float value => (float)_r.NextDouble();
        public static Vector2 insideUnitCircle => new Vector2((float)(_r.NextDouble()*2-1),(float)(_r.NextDouble()*2-1));
        public static Vector3 insideUnitSphere  => new Vector3((float)(_r.NextDouble()*2-1),(float)(_r.NextDouble()*2-1),(float)(_r.NextDouble()*2-1));
        public static Vector3 onUnitSphere      => insideUnitSphere.normalized;
        public static Quaternion rotation       => Quaternion.identity;
        public static Quaternion rotationUniform => Quaternion.identity;
        public static Color ColorHSV() => Color.white;
        public static Color ColorHSV(float hMin, float hMax) => Color.white;
        public static Color ColorHSV(float hMin, float hMax, float sMin, float sMax) => Color.white;
        public static Color ColorHSV(float hMin, float hMax, float sMin, float sMax, float vMin, float vMax) => Color.white;
        public static float Range(float min, float max) => min + (float)_r.NextDouble()*(max-min);
        public static int   Range(int min, int max)     => _r.Next(min, max);
        public struct State { public int s0, s1, s2, s3; }
        public static State state { get; set; }
        public static void InitState(int seed) { }
    }

    public static class Screen
    {
        public static int       width          { get; }
        public static int       height         { get; }
        public static float     dpi            { get; }
        public static Resolution currentResolution { get; }
        public static Resolution[] resolutions { get; }
        public static FullScreenMode fullScreenMode { get; set; }
        public static bool      fullScreen     { get; set; }
        public static int       sleepTimeout   { get; set; }
        public static Orientation orientation  { get; set; }
        public static float     brightness     { get; set; }
        public static Rect      safeArea       { get; }
        public static Rect[]    cutouts        { get; }
        public static void SetResolution(int w, int h, bool fullscreen, int refreshRate = 0) { }
        public static void SetResolution(int w, int h, FullScreenMode mode, int refreshRate = 0) { }
    }
    public enum Orientation { Portrait, PortraitUpsideDown, LandscapeLeft, LandscapeRight, AutoRotation }

    public static class Physics
    {
        public const int DefaultRaycastLayers = -5;
        public const int IgnoreRaycastLayer = 4;
        public static float gravity_y = -9.81f;
        public static Vector3 gravity { get => new Vector3(0, gravity_y, 0); set { gravity_y = value.y; } }
        public static bool autoSimulation { get; set; }
        public static bool autoSyncTransforms { get; set; }
        public static float defaultContactOffset { get; set; }
        public static float defaultSolverIterations { get; set; }
        public static bool bounceThreshold { get; set; }
        public static bool Raycast(Vector3 origin, Vector3 dir, float maxDist = float.MaxValue, int layerMask = DefaultRaycastLayers, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal) => false;
        public static bool Raycast(Vector3 origin, Vector3 dir, out RaycastHit hit, float maxDist = float.MaxValue, int layerMask = DefaultRaycastLayers, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal) { hit = default; return false; }
        public static bool Raycast(Ray ray, out RaycastHit hit, float maxDist = float.MaxValue, int layerMask = DefaultRaycastLayers) { hit = default; return false; }
        public static RaycastHit[] RaycastAll(Vector3 origin, Vector3 dir, float maxDist = float.MaxValue, int layerMask = DefaultRaycastLayers) => new RaycastHit[0];
        public static bool SphereCast(Vector3 origin, float radius, Vector3 dir, out RaycastHit hit, float maxDist = float.MaxValue, int layerMask = DefaultRaycastLayers, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal) { hit = default; return false; }
        public static bool SphereCast(Ray ray, float radius, float maxDist = float.MaxValue, int layerMask = DefaultRaycastLayers) => false;
        public static bool SphereCast(Ray ray, float radius, out RaycastHit hit, float maxDist = float.MaxValue, int layerMask = DefaultRaycastLayers) { hit = default; return false; }
        public static Collider[] OverlapSphere(Vector3 pos, float radius, int layerMask = DefaultRaycastLayers, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal) => new Collider[0];
        public static Collider[] OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation = default, int layerMask = DefaultRaycastLayers, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal) => new Collider[0];
        public static bool CheckSphere(Vector3 pos, float radius, int layerMask = DefaultRaycastLayers, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal) => false;
        public static bool Linecast(Vector3 start, Vector3 end, int layerMask = DefaultRaycastLayers) => false;
        public static bool Linecast(Vector3 start, Vector3 end, out RaycastHit hit, int layerMask = DefaultRaycastLayers) { hit = default; return false; }
        public static bool CapsuleCast(Vector3 p1, Vector3 p2, float radius, Vector3 dir, float maxDist = float.MaxValue, int layerMask = DefaultRaycastLayers) => false;
        public static void IgnoreCollision(Collider a, Collider b, bool ignore = true) { }
        public static void IgnoreLayerCollision(int l1, int l2, bool ignore = true) { }
        public static bool GetIgnoreLayerCollision(int l1, int l2) => false;
        public static int OverlapSphereNonAlloc(Vector3 pos, float radius, Collider[] results, int layerMask = DefaultRaycastLayers, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal) => 0;
        public static int OverlapBoxNonAlloc(Vector3 center, Vector3 halfExtents, Collider[] results, Quaternion orientation = default, int layerMask = DefaultRaycastLayers, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal) => 0;
        public static int RaycastNonAlloc(Ray ray, RaycastHit[] results, float maxDist = float.MaxValue, int layerMask = DefaultRaycastLayers) => 0;
        public static int RaycastNonAlloc(Vector3 origin, Vector3 dir, RaycastHit[] results, float maxDist = float.MaxValue, int layerMask = DefaultRaycastLayers) => 0;
    }

    public static class Gizmos
    {
        public static Color color { get; set; }
        public static Matrix4x4 matrix { get; set; }
        public static void DrawSphere(Vector3 center, float radius) { }
        public static void DrawWireSphere(Vector3 center, float radius) { }
        public static void DrawCube(Vector3 center, Vector3 size) { }
        public static void DrawWireCube(Vector3 center, Vector3 size) { }
        public static void DrawLine(Vector3 from, Vector3 to) { }
        public static void DrawRay(Ray ray) { }
        public static void DrawRay(Vector3 from, Vector3 direction) { }
        public static void DrawFrustum(Vector3 center, float fov, float maxRange, float minRange, float aspect) { }
        public static void DrawMesh(Mesh mesh, Vector3 position = default, Quaternion rotation = default, Vector3 scale = default) { }
        public static void DrawIcon(Vector3 center, string name, bool allowScaling = true) { }
    }

    public static class Physics2D
    {
        public static RaycastHit2D Raycast(Vector2 origin, Vector2 dir, float dist = float.MaxValue, int layerMask = -1) => default;
        public static RaycastHit2D[] RaycastAll(Vector2 origin, Vector2 dir, float dist = float.MaxValue, int layerMask = -1) => new RaycastHit2D[0];
    }
    public struct RaycastHit2D { public Vector2 point, normal; public float distance; public Collider2D collider; public bool collider2D => collider != null; }
    public class Collider2D : Component { public bool isTrigger { get; set; } public bool enabled { get; set; } }

    public struct LayerMask
    {
        public static int GetMask(params string[] layerNames) => 0;
        public static int NameToLayer(string name) => 0;
        public static string LayerToName(int layer) => "";
        public static implicit operator int(LayerMask lm) => lm.value;
        public static implicit operator LayerMask(int v) => new LayerMask { value = v };
        public int value { get; set; }
    }

    // ── Geometry / Mesh helpers ───────────────────────────────────────────────
    public struct BoneWeight1
    {
        public int   boneIndex { get; set; }
        public float weight    { get; set; }
    }

    public struct TextGenerationSettings { }

    public class TextMesh : Component
    {
        public string    text      { get; set; }
        public float     fontSize  { get; set; }
        public Color     color     { get; set; }
        public TextAnchor anchor   { get; set; }
        public float     characterSize { get; set; }
    }

    public class CanvasGroup : MonoBehaviour
    {
        public float alpha          { get; set; }
        public bool  interactable   { get; set; }
        public bool  blocksRaycasts { get; set; }
        public bool  ignoreParentGroups { get; set; }
    }

    public static class PlayerPrefs
    {
        public static void SetInt(string key, int val) { }
        public static int GetInt(string key, int def = 0) => def;
        public static void SetFloat(string key, float val) { }
        public static float GetFloat(string key, float def = 0f) => def;
        public static void SetString(string key, string val) { }
        public static string GetString(string key, string def = "") => def;
        public static bool HasKey(string key) => false;
        public static void DeleteKey(string key) { }
        public static void DeleteAll() { }
        public static void Save() { }
    }

    public static class JsonUtility
    {
        public static string ToJson(object obj, bool prettyPrint = false) => "{}";
        public static T FromJson<T>(string json) => default;
        public static void FromJsonOverwrite(string json, object objectToOverwrite) { }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object => null;
        public static Object Load(string path) => null;
        public static Object Load(string path, Type systemTypeInstance) => null;
        public static T[] LoadAll<T>(string path) where T : Object => new T[0];
        public static Object[] LoadAll(string path) => new Object[0];
        public static void UnloadAsset(Object assetToUnload) { }
        public static AsyncOperation UnloadUnusedAssets() => new AsyncOperation();
        public static ResourceRequest LoadAsync<T>(string path) where T : Object => new ResourceRequest();
        public static ResourceRequest LoadAsync(string path, Type type) => new ResourceRequest();
    }
    public class ResourceRequest : AsyncOperation { public Object asset { get; } }

    public static class GUIUtility
    {
        public static int hotControl { get; set; }
        public static int keyboardControl { get; set; }
        public static string systemCopyBuffer { get; set; }
        public static Vector2 GUIToScreenPoint(Vector2 guiPoint) => guiPoint;
        public static Vector2 ScreenToGUIPoint(Vector2 screenPoint) => screenPoint;
        public static Rect ScreenToGUIRect(Rect screenRect) => screenRect;
        public static void ExitGUI() { }
    }

    public class GUIStyleState
    {
        public Texture2D background { get; set; }
        public Color textColor { get; set; }
    }

    public class GUIStyle
    {
        public GUIStyleState normal    { get; set; } = new GUIStyleState();
        public GUIStyleState hover     { get; set; } = new GUIStyleState();
        public GUIStyleState active    { get; set; } = new GUIStyleState();
        public GUIStyleState focused   { get; set; } = new GUIStyleState();
        public GUIStyleState onNormal  { get; set; } = new GUIStyleState();
        public GUIStyleState onHover   { get; set; } = new GUIStyleState();
        public GUIStyleState onActive  { get; set; } = new GUIStyleState();
        public GUIStyleState onFocused { get; set; } = new GUIStyleState();
        public Font font { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public bool wordWrap { get; set; }
        public bool richText { get; set; }
        public bool clipping { get; set; }
        public TextAnchor alignment { get; set; }
        public float fixedWidth { get; set; }
        public float fixedHeight { get; set; }
        public bool stretchWidth { get; set; }
        public bool stretchHeight { get; set; }
        public RectOffset padding   { get; set; } = new RectOffset();
        public RectOffset margin    { get; set; } = new RectOffset();
        public RectOffset border    { get; set; } = new RectOffset();
        public RectOffset overflow  { get; set; } = new RectOffset();
        public GUIStyle() { }
        public GUIStyle(string styleName) { }
        public GUIStyle(GUIStyle other) { }
        public Vector2 CalcSize(GUIContent c) => Vector2.zero;
        public float CalcHeight(GUIContent c, float w) => 0f;
    }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public class RectOffset { public int left, right, top, bottom; }

    public class GUISkin : ScriptableObject
    {
        public GUIStyle label { get; set; } = new GUIStyle();
        public GUIStyle button { get; set; } = new GUIStyle();
        public GUIStyle textField { get; set; } = new GUIStyle();
        public GUIStyle textArea { get; set; } = new GUIStyle();
        public GUIStyle box { get; set; } = new GUIStyle();
        public GUIStyle toggle { get; set; } = new GUIStyle();
        public GUIStyle window { get; set; } = new GUIStyle();
        public GUIStyle horizontalSlider { get; set; } = new GUIStyle();
        public GUIStyle verticalSlider { get; set; } = new GUIStyle();
        public GUIStyle FindStyle(string styleName) => new GUIStyle();
        public GUIStyle GetStyle(string styleName) => new GUIStyle();
    }

    public class GUIContent
    {
        public string text { get; set; }
        public Texture image { get; set; }
        public string tooltip { get; set; }
        public static GUIContent none => new GUIContent();
        public GUIContent() { }
        public GUIContent(string text) { this.text = text; }
        public GUIContent(Texture image) { this.image = image; }
        public GUIContent(string text, Texture image) { this.text = text; this.image = image; }
        public GUIContent(string text, string tooltip) { this.text = text; this.tooltip = tooltip; }
        public GUIContent(string text, Texture image, string tooltip) { this.text = text; this.image = image; this.tooltip = tooltip; }
        public static implicit operator GUIContent(string s) => new GUIContent(s);
    }

    public static class GUI
    {
        public static GUISkin skin { get; set; }
        public static Color color { get; set; }
        public static Color backgroundColor { get; set; }
        public static Color contentColor { get; set; }
        public static bool enabled { get; set; }
        public static int depth { get; set; }
        public static Matrix4x4 matrix { get; set; }
        public static string tooltip { get; }
        public static bool changed { get; set; }
        public static void Label(Rect pos, string text) { }
        public static void Label(Rect pos, GUIContent content) { }
        public static void Label(Rect pos, string text, GUIStyle style) { }
        public static void Label(Rect pos, GUIContent content, GUIStyle style) { }
        public static void Box(Rect pos, string text) { }
        public static void Box(Rect pos, GUIContent content) { }
        public static void Box(Rect pos, string text, GUIStyle style) { }
        public static void Box(Rect pos, GUIContent content, GUIStyle style) { }
        public static bool Button(Rect pos, string text) => false;
        public static bool Button(Rect pos, GUIContent content) => false;
        public static bool Button(Rect pos, string text, GUIStyle style) => false;
        public static string TextField(Rect pos, string text) => text;
        public static string TextField(Rect pos, string text, GUIStyle style) => text;
        public static string TextArea(Rect pos, string text) => text;
        public static string TextArea(Rect pos, string text, GUIStyle style) => text;
        public static bool Toggle(Rect pos, bool v, string text) => v;
        public static bool Toggle(Rect pos, bool v, GUIContent content) => v;
        public static float HorizontalSlider(Rect pos, float v, float min, float max) => v;
        public static float VerticalSlider(Rect pos, float v, float min, float max) => v;
        public static Vector2 BeginScrollView(Rect pos, Vector2 scrollPos, Rect viewRect) => scrollPos;
        public static void EndScrollView() { }
        public static Rect Window(int id, Rect clientRect, GUI.WindowFunction func, string text) => clientRect;
        public delegate void WindowFunction(int id);
        public static void DragWindow(Rect pos) { }
        public static void DragWindow() { }
        public static void FocusWindow(int id) { }
        public static void BringWindowToFront(int id) { }
        public static void BringWindowToBack(int id) { }
        public static void DrawTexture(Rect pos, Texture image) { }
        public static void DrawTexture(Rect pos, Texture image, ScaleMode scaleMode) { }
        public static void DrawTexture(Rect pos, Texture image, ScaleMode scaleMode, bool alphaBlend) { }
        public static void BeginGroup(Rect pos) { }
        public static void EndGroup() { }
        public static void SetNextControlName(string name) { }
        public static string GetNameOfFocusedControl() => "";
        public static void FocusControl(string name) { }
        public static void UnfocusWindow() { }
    }
    public enum ScaleMode { StretchToFill, ScaleAndCrop, ScaleToFit }

    public static class GUILayout
    {
        public static GUILayoutOption ExpandWidth(bool v)  => null;
        public static GUILayoutOption ExpandHeight(bool v) => null;
        public static GUILayoutOption Width(float w)       => null;
        public static GUILayoutOption Height(float h)      => null;
        public static GUILayoutOption MinWidth(float w)    => null;
        public static GUILayoutOption MaxWidth(float w)    => null;
        public static GUILayoutOption MinHeight(float h)   => null;
        public static GUILayoutOption MaxHeight(float h)   => null;
        public static void Label(string text, params GUILayoutOption[] opts) { }
        public static void Label(string text, GUIStyle style, params GUILayoutOption[] opts) { }
        public static void Label(GUIContent content, params GUILayoutOption[] opts) { }
        public static bool Button(string text, params GUILayoutOption[] opts) => false;
        public static bool Button(string text, GUIStyle style, params GUILayoutOption[] opts) => false;
        public static bool Button(GUIContent content, params GUILayoutOption[] opts) => false;
        public static string TextField(string text, params GUILayoutOption[] opts) => text;
        public static string TextField(string text, GUIStyle style, params GUILayoutOption[] opts) => text;
        public static string TextArea(string text, params GUILayoutOption[] opts) => text;
        public static bool Toggle(bool v, string text, params GUILayoutOption[] opts) => v;
        public static float HorizontalSlider(float v, float min, float max, params GUILayoutOption[] opts) => v;
        public static int Toolbar(int sel, string[] texts, params GUILayoutOption[] opts) => sel;
        public static int SelectionGrid(int sel, string[] texts, int cols, params GUILayoutOption[] opts) => sel;
        public static void BeginHorizontal(params GUILayoutOption[] opts) { }
        public static void BeginHorizontal(GUIStyle style, params GUILayoutOption[] opts) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] opts) { }
        public static void BeginVertical(GUIStyle style, params GUILayoutOption[] opts) { }
        public static void EndVertical() { }
        public static void Space(float pixels) { }
        public static void FlexibleSpace() { }
        public static Rect GetRect(float w, float h) => Rect.zero;
        public static Vector2 BeginScrollView(Vector2 scrollPos, params GUILayoutOption[] opts) => scrollPos;
        public static void EndScrollView() { }
        public static Rect BeginArea(Rect screenRect) => screenRect;
        public static Rect BeginArea(Rect screenRect, string text) => screenRect;
        public static void EndArea() { }
    }
    public class GUILayoutOption { }

    public static class QualitySettings
    {
        public static int   masterTextureLimit { get; set; }
        public static int   antiAliasing       { get; set; }
        public static int   vSyncCount         { get; set; }
        public static float lodBias            { get; set; }
        public static int   maximumLODLevel    { get; set; }
        public static bool  softParticles      { get; set; }
        public static int   shadowCascades     { get; set; }
        public static float shadowDistance     { get; set; }
        public static int   pixelLightCount    { get; set; }
        public static string[] names           { get; }
        public static int   GetQualityLevel()                  => 0;
        public static void  SetQualityLevel(int idx, bool applyExpensiveChanges = true) { }
        public static void  IncreaseLevel(bool applyExpensiveChanges = false) { }
        public static void  DecreaseLevel(bool applyExpensiveChanges = false) { }
    }

    public static class RenderSettings
    {
        public static Rendering.AmbientMode ambientMode  { get; set; }
        public static Color   ambientLight   { get; set; }
        public static Color   ambientSkyColor { get; set; }
        public static Color   ambientEquatorColor { get; set; }
        public static Color   ambientGroundColor { get; set; }
        public static float   ambientIntensity { get; set; }
        public static bool    fog            { get; set; }
        public static Color   fogColor       { get; set; }
        public static float   fogDensity     { get; set; }
        public static FogMode fogMode        { get; set; }
        public static float   fogStartDistance { get; set; }
        public static float   fogEndDistance  { get; set; }
        public static Material skybox         { get; set; }
        public static float   haloStrength    { get; set; }
        public static float   flareStrength   { get; set; }
    }
    public enum FogMode { Linear, Exponential, ExponentialSquared }

    public static class Cursor
    {
        public static CursorLockMode lockState { get; set; }
        public static bool visible { get; set; }
        public static void SetCursor(Texture2D texture, Vector2 hotspot, CursorMode cursorMode) { }
    }
    public enum CursorLockMode { None, Locked, Confined }
    public enum CursorMode { Auto, ForceSoftware }

    public static class ScreenCapture
    {
        public static void CaptureScreenshot(string filename, int supersize = 1) { }
        public static Texture2D CaptureScreenshotAsTexture(int supersize = 1) => new Texture2D(1,1);
        public static AsyncOperation CaptureScreenshotIntoRenderTexture(RenderTexture renderTexture) => new AsyncOperation();
    }

    // ── Component types ───────────────────────────────────────────────────────
    public class Renderer : Component
    {
        public Material   material   { get; set; }
        public Material[] materials  { get; set; }
        public Material   sharedMaterial { get; set; }
        public Material[] sharedMaterials { get; set; }
        public bool       enabled    { get; set; }
        public Bounds     bounds     { get; }
        public int        sortingOrder { get; set; }
        public string     sortingLayerName { get; set; }
        public int        sortingLayerID { get; set; }
        public bool       isVisible  { get; }
        public bool       receiveShadows { get; set; }
        public ShadowCastingMode shadowCastingMode { get; set; }
        public void SetPropertyBlock(MaterialPropertyBlock block) { }
        public void GetPropertyBlock(MaterialPropertyBlock block) { }
    }

    public class MeshRenderer : Renderer { }
    public class SkinnedMeshRenderer : Renderer
    {
        public Mesh sharedMesh { get; set; }
        public Mesh mesh { get; set; }
        public Transform rootBone { get; set; }
        public Transform[] bones { get; set; }
        public int blendShapeCount { get; }
        public void SetBlendShapeWeight(int index, float value) { }
        public float GetBlendShapeWeight(int index) => 0f;
        public void BakeMesh(Mesh mesh) { }
    }

    public class MeshFilter : Component
    {
        public Mesh mesh { get; set; }
        public Mesh sharedMesh { get; set; }
    }

    public class Mesh : Object
    {
        public Vector3[]  vertices     { get; set; }
        public Vector3[]  normals      { get; set; }
        public Vector4[]  tangents     { get; set; }
        public Vector2[]  uv           { get; set; }
        public Vector2[]  uv2          { get; set; }
        public Vector2[]  uv3          { get; set; }
        public Vector2[]  uv4          { get; set; }
        public Color[]    colors       { get; set; }
        public Color32[]  colors32     { get; set; }
        public int[]      triangles    { get; set; }
        public BoneWeight[] boneWeights { get; set; }
        public Matrix4x4[]  bindposes  { get; set; }
        public Bounds     bounds       { get; set; }
        public int        subMeshCount { get; set; }
        public int        vertexCount  { get; }
        public bool       isReadable   { get; }
        public int        blendShapeCount { get; }
        public Rendering.IndexFormat indexFormat  { get; set; }
        public void Clear(bool keepVertexLayout = true) { }
        public void RecalculateNormals() { }
        public void RecalculateBounds() { }
        public void RecalculateTangents() { }
        public void MarkDynamic() { }
        public void UploadMeshData(bool markNoLongerReadable) { }
        public void SetVertices(List<Vector3> verts) { }
        public void SetVertices(Vector3[] verts) { }
        public void SetNormals(List<Vector3> norms) { }
        public void SetNormals(Vector3[] norms) { }
        public void SetUVs(int channel, List<Vector2> uvs) { }
        public void SetUVs(int channel, Vector2[] uvs) { }
        public void SetUVs(int channel, List<Vector4> uvs) { }
        public void SetUVs(int channel, Vector4[] uvs) { }
        public void SetTangents(Vector4[] tangents) { }
        public void SetTangents(List<Vector4> tangents) { }
        public void SetBoneWeights(BoneWeight1[] weights) { }
        public void SetTriangles(List<int> tris, int submesh) { }
        public void SetTriangles(int[] tris, int submesh) { }
        public void SetIndices(int[] indices, MeshTopology topology, int submesh) { }
        public void SetColors(List<Color> colors) { }
        public void SetColors(List<Color32> colors) { }
        public void CombineMeshes(CombineInstance[] combine, bool mergeSubMeshes = true, bool useMatrices = true) { }
        public int GetBlendShapeIndex(string blendShapeName) => -1;
        public string GetBlendShapeName(int shapeIndex) => "";
        public int GetBlendShapeFrameCount(int shapeIndex) => 0;
        public float GetBlendShapeFrameWeight(int shapeIndex, int frameIndex) => 0f;
        public void GetBlendShapeFrameVertices(int shapeIndex, int frameIndex, Vector3[] deltaVertices, Vector3[] deltaNormals, Vector3[] deltaTangents) { }
        public void AddBlendShapeFrame(string shapeName, float frameWeight, Vector3[] deltaVertices, Vector3[] deltaNormals, Vector3[] deltaTangents) { }
        public void GetTriangles(List<int> tris, int submesh) { }
        public int[] GetTriangles(int submesh) => new int[0];
        public void SetSubMesh(int index, SubMeshDescriptor desc, Rendering.MeshUpdateFlags flags = Rendering.MeshUpdateFlags.Default) { }
        public SubMeshDescriptor GetSubMesh(int index) => default;
    }
    public struct SubMeshDescriptor { public int indexStart, indexCount; public MeshTopology topology; public Bounds bounds; public int firstVertex, vertexCount, baseVertex; }
    public struct CombineInstance { public Mesh mesh; public int subMeshIndex; public Matrix4x4 transform; public int lightmapScaleOffset; public int realtimeLightmapScaleOffset; }

    public class Material : Object
    {
        public Shader  shader      { get; set; }
        public Color   color       { get; set; }
        public Texture mainTexture { get; set; }
        public Vector2 mainTextureOffset { get; set; }
        public Vector2 mainTextureScale  { get; set; }
        public int     renderQueue  { get; set; }
        public bool    enableInstancing { get; set; }
        public Material(Shader s) { shader = s; }
        public Material(Material m) { }
        public void SetColor(string name, Color c) { }
        public void SetColor(int id, Color c) { }
        public void SetFloat(string name, float v) { }
        public void SetFloat(int id, float v) { }
        public void SetInt(string name, int v) { }
        public void SetInt(int id, int v) { }
        public void SetVector(string name, Vector4 v) { }
        public void SetVector(int id, Vector4 v) { }
        public void SetMatrix(string name, Matrix4x4 m) { }
        public void SetTexture(string name, Texture t) { }
        public void SetTexture(int id, Texture t) { }
        public Color  GetColor(string name) => Color.white;
        public float  GetFloat(string name) => 0f;
        public int    GetInt(string name) => 0;
        public Vector4 GetVector(string name) => Vector4.zero;
        public Texture GetTexture(string name) => null;
        public bool   HasProperty(string name) => false;
        public bool   HasProperty(int nameId) => false;
        public void   EnableKeyword(string keyword) { }
        public void   DisableKeyword(string keyword) { }
        public bool   IsKeywordEnabled(string keyword) => false;
        public void   CopyPropertiesFromMaterial(Material m) { }
        public static int PropertyToID(string name) => 0;
    }

    public class MaterialPropertyBlock
    {
        public void Clear() { }
        public void SetColor(string name, Color c) { }
        public void SetFloat(string name, float v) { }
        public void SetTexture(string name, Texture t) { }
        public bool isEmpty { get; }
    }

    public class Shader : Object
    {
        public static Shader Find(string name) => new Shader();
        public static int PropertyToID(string name) => 0;
        public bool isSupported { get; }
        public int maximumLOD { get; set; }
    }

    public class Texture : Object
    {
        public int   width       { get; }
        public int   height      { get; }
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
        public int   anisoLevel  { get; set; }
        public float mipMapBias  { get; set; }
        public int   mipmapCount { get; }
        public bool  isReadable  { get; }
        public void Apply(bool updateMipmaps = true, bool makeNoLongerReadable = false) { }
    }
    public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }
    public enum TextureFormat { Alpha8=1, RGB24=3, RGBA32=4, ARGB32=5, RGB565=7, R16=9, DXT1=10, DXT5=12, RGBA4444=13, BGRA32=14, RGBAHalf=17, RGBAFloat=34, YUY2=21, PVRTC_RGB2=30, PVRTC_RGBA2=31, PVRTC_RGB4=32, PVRTC_RGBA4=33, ETC_RGB4=34, EAC_R=41, EAC_R_SIGNED=42, EAC_RG=43, EAC_RG_SIGNED=44, ETC2_RGB=45, ETC2_RGBA1=46, ETC2_RGBA8=47, BC4=26, BC5=27, BC6H=24, BC7=25, R8=63, RG16=62, RG32=39, RGB48=40, RGBA64=35 }

    public class Texture2D : Texture
    {
        public Texture2D(int w, int h) { }
        public Texture2D(int w, int h, TextureFormat fmt, bool mips) { }
        public Texture2D(int w, int h, TextureFormat fmt, bool mips, bool linear) { }
        public void SetPixel(int x, int y, Color c) { }
        public void SetPixels(Color[] pixels) { }
        public void SetPixels(Color[] pixels, int mip) { }
        public void SetPixels32(Color32[] pixels) { }
        public void SetPixels32(int x, int y, int w, int h, Color32[] pixels, int mip = 0) { }
        public Color GetPixel(int x, int y) => Color.white;
        public Color[] GetPixels() => new Color[0];
        public Color[] GetPixels(int x, int y, int blockWidth, int blockHeight, int mip = 0) => new Color[0];
        public Color32[] GetPixels32(int mip = 0) => new Color32[0];
        public void LoadRawTextureData(byte[] data) { }
        public void LoadRawTextureData(System.IntPtr data, int size) { }
        public byte[] GetRawTextureData() => new byte[0];
        public void Apply(bool updateMipmaps = true, bool makeNoLongerReadable = false) { }
        public bool LoadImage(byte[] data, bool markNonReadable = false) => true;
        public byte[] EncodeToPNG() => new byte[0];
        public byte[] EncodeToJPG(int quality = 75) => new byte[0];
        public Rect[] PackTextures(Texture2D[] textures, int padding, int maximumAtlasSize = 2048, bool makeNoLongerReadable = false) => new Rect[0];
        public static Texture2D whiteTexture { get; }
        public static Texture2D blackTexture { get; }
        public static Texture2D normalTexture { get; }
        public bool alphaIsTransparency { get; set; }
        public void Resize(int w, int h) { }
        public void Compress(bool highQuality) { }
        public void ReadPixels(Rect source, int destX, int destY, bool recalculateMipMaps = true) { }
    }

    public class RenderTexture : Texture
    {
        public int   depth      { get; set; }
        public bool  isCreated  { get; }
        public bool  useMipMap  { get; set; }
        public bool  autoGenerateMips { get; set; }
        public RenderTextureDescriptor descriptor { get; set; }
        public static RenderTexture active { get; set; }
        public RenderTexture(int w, int h, int depth) { }
        public RenderTexture(int w, int h, int depth, RenderTextureFormat fmt) { }
        public RenderTexture(RenderTextureDescriptor desc) { }
        public bool Create() => true;
        public void Release() { }
        public void DiscardContents() { }
        public bool IsCreated() => false;
    }
    public struct RenderTextureDescriptor { public int width, height, depth, volumeDepth, msaaSamples; public RenderTextureFormat colorFormat; public bool sRGB, useMipMap, autoGenerateMips; }
    public enum RenderTextureFormat { Default, ARGB32, Depth, ARGBHalf, Shadowmap, RGB565, ARGB4444, ARGB1555, Default_HDR, ARGB2101010, DefaultHDR, RGBAUShort, RG16, R8, ARGBInt, RGInt, RInt, BGRA32, RGB111110Float, RG32, RGBAUShort4, RG16Signed, RGB48, Rgba64, R16, RGHalf, RFloat, RGFloat, RGBAFloat, YUV2 }

    public class Cubemap : Texture
    {
        public Cubemap(int size, TextureFormat fmt, bool mips) { }
        public void Apply() { }
    }

    public class Sprite : Object
    {
        public Rect rect { get; }
        public Texture2D texture { get; }
        public Vector2 pivot { get; }
        public Bounds bounds { get; }
        public float pixelsPerUnit { get; }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit = 100f) => new Sprite();
    }

    public class Camera : Behaviour
    {
        public static Camera main { get; }
        public static Camera current { get; }
        public static int allCamerasCount { get; }
        public static Camera[] allCameras { get; }
        public float  fieldOfView     { get; set; }
        public float  nearClipPlane   { get; set; }
        public float  farClipPlane    { get; set; }
        public float  aspect          { get; set; }
        public float  orthographicSize { get; set; }
        public bool   orthographic    { get; set; }
        public ClearFlags clearFlags  { get; set; }
        public Color  backgroundColor { get; set; }
        public int    cullingMask     { get; set; }
        public int    depth           { get; set; }
        public RenderingPath renderingPath { get; set; }
        public RenderTexture targetTexture { get; set; }
        public bool   useOcclusionCulling { get; set; }
        public bool   allowHDR          { get; set; }
        public bool   allowMSAA         { get; set; }
        public DepthTextureMode depthTextureMode { get; set; }
        public Matrix4x4 worldToCameraMatrix { get; set; }
        public Matrix4x4 projectionMatrix { get; set; }
        public Matrix4x4 nonJitteredProjectionMatrix { get; }
        public Matrix4x4 cameraToWorldMatrix { get; }
        public float pixelWidth { get; }
        public float pixelHeight { get; }
        public Rect  rect { get; set; }
        public Rect  pixelRect { get; }
        public bool  stereoEnabled { get; }
        public float stereoSeparation { get; set; }
        public CameraType cameraType { get; set; }
        public int eventMask { get; set; }
        public bool forceIntoRenderTexture { get; set; }
        public Vector3 WorldToScreenPoint(Vector3 pos) => pos;
        public Vector3 WorldToViewportPoint(Vector3 pos) => pos;
        public Vector3 ScreenToWorldPoint(Vector3 pos) => pos;
        public Vector3 ScreenToViewportPoint(Vector3 pos) => pos;
        public Vector3 ViewportToWorldPoint(Vector3 pos) => pos;
        public Vector3 ViewportToScreenPoint(Vector3 pos) => pos;
        public Ray    ScreenPointToRay(Vector3 pos) => new Ray(pos, Vector3.forward);
        public Ray    ViewportPointToRay(Vector3 pos) => new Ray(pos, Vector3.forward);
        public bool   WorldToScreenPoint(Vector3 worldPos, out Vector3 screenPos) { screenPos = worldPos; return true; }
        public void   Render() { }
        public void   RenderWithShader(Shader s, string tag) { }
        public bool   RenderToCubemap(Cubemap cubemap) => false;
        public void   CopyFrom(Camera other) { }
        public void   ResetProjectionMatrix() { }
        public void   ResetWorldToCameraMatrix() { }
        public void   ResetAspect() { }
        public static void SetupCurrent(Camera cur) { }
        public Plane[] CalculateFrustumPlanes() => new Plane[6];
        public static void CalculateFrustumPlanes(Camera c, Plane[] planes) { }
    }

    public class Light : Behaviour
    {
        public LightType    type         { get; set; }
        public Color        color        { get; set; }
        public float        intensity    { get; set; }
        public float        range        { get; set; }
        public float        spotAngle    { get; set; }
        public LightShadows shadows      { get; set; }
        public float        shadowStrength { get; set; }
        public float        shadowBias   { get; set; }
        public float        shadowNormalBias { get; set; }
        public bool         enabled      { get; set; }
        public int          cullingMask  { get; set; }
        public float        bounceIntensity { get; set; }
    }

    public class Collider : Component
    {
        public bool    isTrigger  { get; set; }
        public bool    enabled    { get; set; }
        public Bounds  bounds     { get; }
        public Material sharedMaterial { get; set; }
        public bool    ClosestPoint(Vector3 pos, out Vector3 result) { result = pos; return false; }
        public Vector3 ClosestPoint(Vector3 pos) => pos;
        public Vector3 ClosestPointOnBounds(Vector3 pos) => pos;
        public bool    Raycast(Ray ray, out RaycastHit hit, float maxDist) { hit = default; return false; }
    }
    public class BoxCollider      : Collider { public Vector3 center { get; set; } public Vector3 size { get; set; } }
    public class SphereCollider   : Collider { public Vector3 center { get; set; } public float radius { get; set; } }
    public class CapsuleCollider  : Collider { public Vector3 center { get; set; } public float radius { get; set; } public float height { get; set; } public int direction { get; set; } }
    public enum MeshColliderCookingOptions { None = 0, CookForFasterSimulation = 4, EnableMeshCleaning = 8, WeldColocatedVertices = 16, UseFastMidphase = 32 }
    public class MeshCollider : Collider
    {
        public Mesh sharedMesh { get; set; }
        public bool convex { get; set; }
        public MeshColliderCookingOptions cookingOptions { get; set; }
    }

    public class Rigidbody : Component
    {
        public Vector3    velocity          { get; set; }
        public Vector3    angularVelocity   { get; set; }
        public float      mass              { get; set; }
        public float      drag              { get; set; }
        public float      angularDrag       { get; set; }
        public bool       useGravity        { get; set; }
        public bool       isKinematic       { get; set; }
        public bool       freezeRotation    { get; set; }
        public RigidbodyConstraints constraints { get; set; }
        public CollisionDetectionMode collisionDetectionMode { get; set; }
        public RigidbodyInterpolation interpolation { get; set; }
        public Vector3    position          { get; set; }
        public Quaternion rotation          { get; set; }
        public Vector3    centerOfMass      { get; set; }
        public Vector3    worldCenterOfMass { get; }
        public float      inertiaTensor     { get; set; }
        public bool       detectCollisions  { get; set; }
        public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force) { }
        public void AddForce(float x, float y, float z, ForceMode mode = ForceMode.Force) { }
        public void AddTorque(Vector3 torque, ForceMode mode = ForceMode.Force) { }
        public void AddRelativeForce(Vector3 force, ForceMode mode = ForceMode.Force) { }
        public void AddRelativeTorque(Vector3 torque, ForceMode mode = ForceMode.Force) { }
        public void AddForceAtPosition(Vector3 force, Vector3 pos, ForceMode mode = ForceMode.Force) { }
        public void AddExplosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius, float upwardsModifier = 0, ForceMode mode = ForceMode.Force) { }
        public void MovePosition(Vector3 pos) { }
        public void MoveRotation(Quaternion rot) { }
        public void Sleep() { }
        public void WakeUp() { }
        public bool IsSleeping() => false;
        public Vector3 GetPointVelocity(Vector3 worldPoint) => Vector3.zero;
        public Vector3 GetRelativePointVelocity(Vector3 relativePoint) => Vector3.zero;
        public bool SweepTest(Vector3 direction, out RaycastHit hitInfo, float maxDistance = float.MaxValue) { hitInfo = default; return false; }
    }

    public class Collision { public Collider collider { get; } public Rigidbody rigidbody { get; } public ContactPoint[] contacts { get; } public Vector3 relativeVelocity { get; } public GameObject gameObject { get; } public Transform transform { get; } public int contactCount { get; } }
    public struct ContactPoint { public Vector3 point, normal; public Collider thisCollider, otherCollider; public float separation; }

    public class CharacterController : Collider
    {
        public float   height        { get; set; }
        public float   radius        { get; set; }
        public Vector3 center        { get; set; }
        public float   slopeLimit    { get; set; }
        public float   stepOffset    { get; set; }
        public float   skinWidth     { get; set; }
        public bool    detectCollisions { get; set; }
        public bool    isGrounded    { get; }
        public CollisionFlags Move(Vector3 motion) => CollisionFlags.None;
        public bool    SimpleMove(Vector3 speed) => false;
        public Vector3 velocity      { get; }
    }
    public enum CollisionFlags { None=0, Sides=1, Above=2, Below=4 }

    public class AudioSource : Behaviour
    {
        public AudioClip  clip           { get; set; }
        public float      volume         { get; set; }
        public float      pitch          { get; set; }
        public bool       loop           { get; set; }
        public bool       playOnAwake    { get; set; }
        public bool       isPlaying      { get; }
        public bool       mute           { get; set; }
        public float      time           { get; set; }
        public float      panStereo      { get; set; }
        public float      spatialBlend   { get; set; }
        public float      minDistance    { get; set; }
        public float      maxDistance    { get; set; }
        public bool       spatialize     { get; set; }
        public AudioRolloffMode rolloffMode { get; set; }
        public float      dopplerLevel   { get; set; }
        public AudioMixerGroup outputAudioMixerGroup { get; set; }
        public void Play(ulong delay = 0) { }
        public void PlayOneShot(AudioClip c, float vol = 1f) { }
        public void PlayDelayed(float delay) { }
        public void PlayScheduled(double time) { }
        public void Stop() { }
        public void Pause() { }
        public void UnPause() { }
        public static void PlayClipAtPoint(AudioClip c, Vector3 pos, float vol = 1f) { }
    }

    public class AudioClip : Object
    {
        public float  length        { get; }
        public int    samples       { get; }
        public int    channels      { get; }
        public int    frequency     { get; }
        public bool   preloadAudioData { get; }
        public bool   loadInBackground { get; }
        public AudioClipLoadType loadType { get; }
        public bool  GetData(float[] data, int offsetSamples) => false;
        public bool  SetData(float[] data, int offsetSamples) => false;
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) => new AudioClip();
    }
    public enum AudioClipLoadType { DecompressOnLoad, CompressedInMemory, Streaming }

    public class AudioListener : Behaviour
    {
        public static float volume { get; set; }
        public static bool pause { get; set; }
    }

    // AudioMixer stubs
    public class AudioMixerGroup : Object { }
    public class AudioMixer : Object
    {
        public bool SetFloat(string name, float value) => true;
        public bool GetFloat(string name, out float value) { value = 0f; return true; }
        public bool ClearFloat(string name) => true;
    }

    public class Animator : Behaviour
    {
        public bool  isInitialized   { get; }
        public bool  hasRootMotion   { get; }
        public bool  applyRootMotion { get; set; }
        public float speed           { get; set; }
        public bool  updateMode      { get; set; }
        public RuntimeAnimatorController runtimeAnimatorController { get; set; }
        public Avatar avatar         { get; set; }
        public bool  isHuman         { get; }
        public bool  isOptimizable   { get; }
        public Vector3 deltaPosition { get; }
        public Quaternion deltaRotation { get; }
        public Vector3 velocity      { get; }
        public Vector3 angularVelocity { get; }
        public Vector3 rootPosition  { get; set; }
        public Quaternion rootRotation { get; set; }
        public bool  stabilizeFeet   { get; set; }
        public int   layerCount      { get; }
        public int   parameterCount  { get; }
        public int   controllerParameterCount { get; }
        public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer) => default;
        public AnimatorStateInfo GetNextAnimatorStateInfo(int layer) => default;
        public AnimatorTransitionInfo GetAnimatorTransitionInfo(int layer) => default;
        public bool IsInTransition(int layer) => false;
        public bool HasState(int layer, int stateHash) => false;
        public void Play(string stateName, int layer = -1, float normalizedTime = float.NegativeInfinity) { }
        public void Play(int stateHash, int layer = -1, float normalizedTime = float.NegativeInfinity) { }
        public void CrossFade(string stateName, float normalizedTransitionDuration, int layer = -1) { }
        public void CrossFade(int stateHash, float normalizedTransitionDuration, int layer = -1) { }
        public void CrossFadeInFixedTime(string stateName, float fixedTransitionDuration, int layer = -1) { }
        public void SetTrigger(string name) { }
        public void SetTrigger(int id) { }
        public void ResetTrigger(string name) { }
        public void SetBool(string name, bool v) { }
        public bool GetBool(string name) => false;
        public void SetFloat(string name, float v) { }
        public void SetFloat(string name, float v, float dampTime, float dt) { }
        public void SetFloat(int id, float v) { }
        public float GetFloat(string name) => 0f;
        public float GetFloat(int id) => 0f;
        public void SetInteger(string name, int v) { }
        public int GetInteger(string name) => 0;
        public void SetLayerWeight(int layer, float w) { }
        public float GetLayerWeight(int layer) => 0f;
        public int GetLayerIndex(string layerName) => 0;
        public string GetLayerName(int layerIndex) => "";
        public AnimatorClipInfo[] GetCurrentAnimatorClipInfo(int layer) => new AnimatorClipInfo[0];
        public void GetCurrentAnimatorClipInfo(int layer, List<AnimatorClipInfo> clips) { }
        public float GetCurrentAnimatorStateInfo(int layer, int shortNameHash) => 0f;
        public void Rebind() { }
        public void Update(float deltaTime) { }
        public void WriteDefaultValues() { }
        public Transform GetBoneTransform(HumanBodyBones humanBoneId) => null;
        public static int StringToHash(string name) => name?.GetHashCode() ?? 0;
        public AnimatorParameter GetParameter(int index) => default;
        public AnimatorControllerParameter[] parameters { get; }
    }
    public struct AnimatorStateInfo { public bool IsName(string n) => false; public int shortNameHash { get; } public int fullPathHash { get; } public float normalizedTime { get; } public float length { get; } public float speed { get; } public float speedMultiplier { get; } public bool loop { get; } public bool IsTag(string t) => false; }
    public struct AnimatorTransitionInfo { public bool IsName(string n) => false; public bool IsUserName(string n) => false; public float normalizedTime { get; } public float duration { get; } public float durationUnit { get; } public bool anyState { get; } }
    public struct AnimatorClipInfo { public AnimationClip clip { get; } public float weight { get; } }
    public struct AnimatorParameter { public string name { get; } public AnimatorControllerParameterType type { get; } public float defaultFloat { get; } public int defaultInt { get; } public bool defaultBool { get; } }
    public class AnimatorControllerParameter { public string name; public AnimatorControllerParameterType type; public float defaultFloat; public int defaultInt; public bool defaultBool; }
    public enum AnimatorControllerParameterType { Float, Int, Bool, Trigger }
    public enum HumanBodyBones { Hips, LeftUpperLeg, RightUpperLeg, LeftLowerLeg, RightLowerLeg, LeftFoot, RightFoot, Spine, Chest, Neck, Head, LeftShoulder, RightShoulder, LeftUpperArm, RightUpperArm, LeftLowerArm, RightLowerArm, LeftHand, RightHand, LeftToes, RightToes, LeftEye, RightEye, Jaw, LastBone }
    public class RuntimeAnimatorController : Object { }
    public class Avatar : Object { }
    public class AnimationClip : Object
    {
        public float       length     { get; }
        public bool        legacy     { get; set; }
        public bool        isLooping  { get; }
        public WrapMode    wrapMode   { get; set; }
        public float       frameRate  { get; set; }
        public void AddEvent(AnimationEvent e) { }
        public void EnsureQuaternionContinuity() { }
        public AnimationEvent[] events { get; set; }
        public void SetCurve(string relativePath, Type type, string propertyName, AnimationCurve curve) { }
    }
    public class AnimationEvent { public string functionName { get; set; } public float time { get; set; } public float floatParameter { get; set; } public int intParameter { get; set; } public string stringParameter { get; set; } public Object objectReferenceParameter { get; set; } }
    public class AnimationCurve
    {
        public Keyframe[] keys { get; set; }
        public WrapMode preWrapMode { get; set; }
        public WrapMode postWrapMode { get; set; }
        public int length { get; }
        public float Evaluate(float time) => 0f;
        public int AddKey(float time, float value) => 0;
        public int AddKey(Keyframe key) => 0;
        public void RemoveKey(int index) { }
        public static AnimationCurve Linear(float timeStart, float valueStart, float timeEnd, float valueEnd) => new AnimationCurve();
        public static AnimationCurve EaseInOut(float timeStart, float valueStart, float timeEnd, float valueEnd) => new AnimationCurve();
        public static AnimationCurve Constant(float timeStart, float timeEnd, float value) => new AnimationCurve();
    }
    public struct Keyframe { public float time, value, inTangent, outTangent, inWeight, outWeight; public int weightedMode; public Keyframe(float t, float v) { time=t; value=v; inTangent=outTangent=inWeight=outWeight=0; weightedMode=0; } }

    public class Animation : Behaviour
    {
        public AnimationClip clip { get; set; }
        public bool   playAutomatically { get; set; }
        public WrapMode wrapMode { get; set; }
        public bool   isPlaying { get; }
        public void Play() { }
        public void Play(string animation) { }
        public void Stop() { }
        public void Stop(string animation) { }
        public void CrossFade(string animation, float fadeLength = 0.3f) { }
        public void Blend(string animation, float targetWeight = 1f, float fadeLength = 0.3f) { }
        public void Rewind(string animation) { }
        public void Rewind() { }
        public AnimationClip GetClip(string name) => null;
        public void AddClip(AnimationClip clip, string newName) { }
        public AnimationState this[string name] { get => null; }
        public bool IsPlaying(string animation) => false;
        public IEnumerator GetEnumerator() => new List<AnimationState>().GetEnumerator();
    }
    public class AnimationState
    {
        public string        name   { get; set; }
        public float         time   { get; set; }
        public float         speed  { get; set; }
        public float         weight { get; set; }
        public float         length { get; }
        public bool          enabled { get; set; }
        public WrapMode      wrapMode { get; set; }
        public AnimationClip clip   { get; set; }
        public int           layer  { get; set; }
        public AnimationBlendMode blendMode { get; set; }
    }

    public class Font : Object
    {
        public int    fontSize      { get; }
        public bool   dynamic      { get; }
        public CharacterInfo[] characterInfo { get; set; }
        public void GetCharacterInfo(char ch, out CharacterInfo info, int size = 0, FontStyle style = FontStyle.Normal) { info = default; }
        public bool HasCharacter(char c) => false;
        public static Font CreateDynamicFontFromOSFont(string fontname, int size) => null;
        public static string[] GetOSInstalledFontNames() => new string[0];
        public static string[] GetPathsToOSFonts() => new string[0];
    }
    public struct CharacterInfo { public char index; public Rect uv, vert; public float width; public int size; public FontStyle style; public bool flipped; }

    public class ParticleSystem : Component
    {
        public bool   isPlaying    { get; }
        public bool   isStopped    { get; }
        public bool   isPaused     { get; }
        public bool   isEmitting   { get; }
        public float  time         { get; set; }
        public int    particleCount { get; }
        public void Play(bool withChildren = true) { }
        public void Stop(bool withChildren = true, ParticleSystemStopBehavior stopBehavior = ParticleSystemStopBehavior.StopEmitting) { }
        public void Pause(bool withChildren = true) { }
        public void Clear(bool withChildren = true) { }
        public void Emit(int count) { }
        public MainModule main { get; }
        public EmissionModule emission { get; }
        public struct MainModule { public float duration { get; set; } public bool loop { get; set; } public MinMaxCurve startLifetime { get; set; } public MinMaxCurve startSpeed { get; set; } public MinMaxGradient startColor { get; set; } public MinMaxCurve startSize { get; set; } public int maxParticles { get; set; } }
        public struct EmissionModule { public bool enabled { get; set; } public MinMaxCurve rateOverTime { get; set; } public MinMaxCurve rateOverDistance { get; set; } }
        public struct MinMaxCurve { public float constant { get; set; } public MinMaxCurve(float v) { constant=v; } }
        public struct MinMaxGradient { public Color color { get; set; } public MinMaxGradient(Color c) { color=c; } }
    }
    public enum ParticleSystemStopBehavior { StopEmitting, StopEmittingAndClear }

    public class LineRenderer : Renderer
    {
        public int   positionCount { get; set; }
        public float startWidth    { get; set; }
        public float endWidth      { get; set; }
        public Color startColor    { get; set; }
        public Color endColor      { get; set; }
        public bool  useWorldSpace { get; set; }
        public bool  loop          { get; set; }
        public void SetPosition(int idx, Vector3 pos) { }
        public void SetPositions(Vector3[] positions) { }
        public Vector3 GetPosition(int idx) => Vector3.zero;
        public void GetPositions(Vector3[] positions) { }
    }

    public class SpriteRenderer : Renderer
    {
        public Sprite sprite    { get; set; }
        public Color  color     { get; set; }
        public bool   flipX     { get; set; }
        public bool   flipY     { get; set; }
        public SpriteSortPoint spriteSortPoint { get; set; }
        public DrawMode drawMode { get; set; }
        public Vector2 size     { get; set; }
    }
    public enum SpriteSortPoint { Center, Pivot }
    public enum DrawMode { Simple, Sliced, Tiled }

    public class UnityException : Exception
    {
        public UnityException() : base() { }
        public UnityException(string msg) : base(msg) { }
        public UnityException(string msg, Exception inner) : base(msg, inner) { }
    }
    public class MissingReferenceException : UnityException { public MissingReferenceException(string msg = "") : base(msg) { } }
    public class MissingComponentException : UnityException { public MissingComponentException(string msg = "") : base(msg) { } }

    [AttributeUsage(AttributeTargets.Field)]   public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)]   public class NonSerialized  : Attribute { }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=false)]  public class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=true)]   public class RequireComponent : Attribute { public RequireComponent(Type t) { } public RequireComponent(Type t1, Type t2) { } public RequireComponent(Type t1, Type t2, Type t3) { } }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)] public class AddComponentMenu : Attribute { public AddComponentMenu(string menu, int order = 0) { } }
    [AttributeUsage(AttributeTargets.Class)]   public class CreateAssetMenu : Attribute { public string fileName { get; set; } public string menuName { get; set; } public int order { get; set; } }
    [AttributeUsage(AttributeTargets.Field)]   public class Header : Attribute { public Header(string h) { } }
    [AttributeUsage(AttributeTargets.Field)]   public class Tooltip : Attribute { public Tooltip(string t) { } }
    [AttributeUsage(AttributeTargets.Field)]   public class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } public RangeAttribute(int min, int max) { } }
    [AttributeUsage(AttributeTargets.Field)]   public class HideInInspector : Attribute { }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)] public class ExecuteInEditMode : Attribute { }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)] public class ExecuteAlways : Attribute { }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class)]  public class ContextMenuItem : Attribute { public ContextMenuItem(string n, string f) { } }
    [AttributeUsage(AttributeTargets.Method)] public class ContextMenu : Attribute { public ContextMenu(string n, bool validate = false, int priority = 1000) { } }
    [AttributeUsage(AttributeTargets.Method)] public class RuntimeInitializeOnLoadMethod : Attribute { public RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType loadType = RuntimeInitializeLoadType.AfterSceneLoad) { } }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }

    [AttributeUsage(AttributeTargets.Class)] public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }
    [AttributeUsage(AttributeTargets.Class)] public class SelectionBase : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class Min : Attribute { public Min(float min) { } }
    [AttributeUsage(AttributeTargets.Field)] public class Multiline : Attribute { public Multiline(int lines = 3) { } }
    [AttributeUsage(AttributeTargets.Field)] public class TextArea : Attribute { public TextArea() { } public TextArea(int minLines, int maxLines) { } }
    [AttributeUsage(AttributeTargets.Field)] public class ColorUsage : Attribute { public ColorUsage(bool showAlpha, bool hdr = false) { } }
    [AttributeUsage(AttributeTargets.Field)] public class GradientUsage : Attribute { public GradientUsage(bool hdr) { } }
    [AttributeUsage(AttributeTargets.Field)] public class Delayed : Attribute { }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class)] public class InspectorName : Attribute { public InspectorName(string displayName) { } }

    public class Gradient
    {
        public GradientColorKey[] colorKeys { get; set; }
        public GradientAlphaKey[] alphaKeys { get; set; }
        public GradientMode mode { get; set; }
        public Color Evaluate(float time) => Color.white;
    }
    public struct GradientColorKey { public Color color; public float time; public GradientColorKey(Color c, float t) { color=c; time=t; } }
    public struct GradientAlphaKey { public float alpha, time; public GradientAlphaKey(float a, float t) { alpha=a; time=t; } }
    public enum GradientMode { Blend, Fixed }

    public class AnimationState2 { }

    // ── IMGUI Event (runtime) ─────────────────────────────────────────────────
    public enum EventType { MouseDown=0, MouseUp=1, MouseMove=2, MouseDrag=3, KeyDown=4, KeyUp=5, ScrollWheel=6, Repaint=7, Layout=8, DragUpdated=9, DragPerform=10, DragExited=15, Ignore=11, Used=12, ValidateCommand=13, ExecuteCommand=14, TouchDown=30, TouchUp=31, TouchMove=32, TouchEnter=33, TouchLeave=34, TouchStationary=35 }
    public class Event
    {
        public static Event current { get; }
        public EventType    type    { get; set; }
        public KeyCode      keyCode { get; set; }
        public char         character { get; set; }
        public Vector2      mousePosition { get; }
        public Vector2      delta         { get; }
        public bool         isKey         { get; }
        public bool         isMouse       { get; }
        public bool         shift         { get; }
        public bool         control       { get; }
        public bool         alt           { get; }
        public bool         command       { get; }
        public bool         capsLock      { get; }
        public int          button        { get; }
        public int          clickCount    { get; }
        public float        pressure      { get; }
        public void Use() { }
        public static Event KeyboardEvent(string key) => new Event();
    }
} // end namespace UnityEngine


// =============================================================================
//  UnityEngine.UI
// =============================================================================
namespace UnityEngine.UI
{
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public enum CanvasScalerMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }

    public class Canvas : UnityEngine.Component
    {
        public RenderMode    renderMode   { get; set; }
        public float         planeDistance { get; set; }
        public int           sortingOrder  { get; set; }
        public int           renderOrder   { get; }
        public bool          overrideSorting { get; set; }
        public bool          pixelPerfect  { get; set; }
        public float         scaleFactor   { get; set; }
        public float         referencePixelsPerUnit { get; set; }
        public bool          isRootCanvas  { get; }
        public UnityEngine.Camera worldCamera { get; set; }
        public UnityEngine.RectTransform GetComponent_RectTransform() => null;
        public static void ForceUpdateCanvases() { }
        public static UnityEngine.GameObject gameObject { get; }
    }

    public class CanvasScaler : UnityEngine.MonoBehaviour
    {
        public CanvasScalerMode uiScaleMode { get; set; }
        public float            scaleFactor { get; set; }
        public float            referencePixelsPerUnit { get; set; }
        public UnityEngine.Vector2 referenceResolution { get; set; }
        public float            matchWidthOrHeight { get; set; }
        public ScreenMatchMode  screenMatchMode { get; set; }
        public float            physicalUnit { get; set; }
        public float            fallbackScreenDPI { get; set; }
        public float            defaultSpriteDPI { get; set; }
    }
    public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }

    public class GraphicRaycaster : UnityEngine.MonoBehaviour
    {
        public bool  ignoreReversedGraphics { get; set; }
        public bool  blockingObjects { get; set; }
    }

    public abstract class Graphic : UnityEngine.MonoBehaviour
    {
        public UnityEngine.Color color     { get; set; }
        public bool  raycastTarget         { get; set; }
        public bool  raycastPadding        { get; set; }
        public UnityEngine.Material material { get; set; }
        public UnityEngine.UI.Canvas canvas   { get; }
        public UnityEngine.RectTransform rectTransform { get; }
        public void SetAllDirty() { }
        public void SetLayoutDirty() { }
        public void SetVerticesDirty() { }
        public void SetMaterialDirty() { }
        public virtual void Rebuild(CanvasUpdate executing) { }
        public virtual void CrossFadeColor(UnityEngine.Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha) { }
        public virtual void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale) { }
    }
    public enum CanvasUpdate { Prelayout, Layout, PostLayout, PreRender, LatePreRender, MaxUpdateValue }

    public abstract class MaskableGraphic : Graphic { public bool maskable { get; set; } }

    public class Image : MaskableGraphic
    {
        public UnityEngine.Sprite sprite     { get; set; }
        public ImageType          type       { get; set; }
        public bool               preserveAspect { get; set; }
        public bool               fillCenter { get; set; }
        public FillMethod         fillMethod { get; set; }
        public float              fillAmount { get; set; }
        public bool               fillClockwise { get; set; }
        public int                fillOrigin { get; set; }
        public bool               useSpriteMesh { get; set; }
        public float              alphaHitTestMinimumThreshold { get; set; }
        public UnityEngine.Texture overrideSprite { get; set; }
        public void SetNativeSize() { }
    }
    public enum ImageType { Simple, Sliced, Tiled, Filled }
    public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }

    public class RawImage : MaskableGraphic
    {
        public UnityEngine.Texture texture   { get; set; }
        public UnityEngine.Rect    uvRect    { get; set; }
    }

    public class Text : MaskableGraphic
    {
        public string    text         { get; set; }
        public UnityEngine.Font font  { get; set; }
        public int       fontSize     { get; set; }
        public UnityEngine.FontStyle fontStyle { get; set; }
        public UnityEngine.TextAnchor alignment { get; set; }
        public bool      resizeTextForBestFit { get; set; }
        public int       resizeTextMinSize { get; set; }
        public int       resizeTextMaxSize { get; set; }
        public bool      supportRichText { get; set; }
        public float     lineSpacing  { get; set; }
        public bool      wordWrap     { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode   verticalOverflow   { get; set; }
        public TextGenerationSettings GetGenerationSettings(UnityEngine.Vector2 extents) => default;
    }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode   { Truncate, Overflow }

    public class Button : UnityEngine.MonoBehaviour
    {
        public bool interactable { get; set; }
        public class ButtonClickedEvent : UnityEngine.Events.UnityEvent { }
        public ButtonClickedEvent onClick = new ButtonClickedEvent();
        public ColorBlock     colors    { get; set; }
        public SpriteState    spriteState { get; set; }
        public Image targetGraphic { get; set; }
        public void Select() { }
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e) { }
    }

    public class Toggle : UnityEngine.MonoBehaviour
    {
        public bool  isOn        { get; set; }
        public bool  interactable { get; set; }
        public class ToggleEvent : UnityEngine.Events.UnityEvent<bool> { }
        public ToggleEvent onValueChanged = new ToggleEvent();
        public ToggleGroup group { get; set; }
        public Image  targetGraphic { get; set; }
        public Graphic graphic { get; set; }
    }

    public class ToggleGroup : UnityEngine.MonoBehaviour
    {
        public bool allowSwitchOff { get; set; }
        public Toggle GetFirstActiveToggle() => null;
        public IEnumerable<Toggle> ActiveToggles() => new List<Toggle>();
        public bool AnyTogglesOn() => false;
        public void SetAllTogglesOff(bool sendCallback = true) { }
        public void RegisterToggle(Toggle toggle) { }
        public void UnregisterToggle(Toggle toggle) { }
    }

    public class Slider : UnityEngine.MonoBehaviour
    {
        public float value    { get; set; }
        public float minValue { get; set; }
        public float maxValue { get; set; }
        public bool  wholeNumbers { get; set; }
        public bool  interactable { get; set; }
        public Direction direction { get; set; }
        public class SliderEvent : UnityEngine.Events.UnityEvent<float> { }
        public SliderEvent onValueChanged = new SliderEvent();
        public Image fillRect   { get; set; }
        public UnityEngine.RectTransform handleRect { get; set; }
    }
    public enum Direction { LeftToRight, RightToLeft, BottomToTop, TopToBottom }

    public class Scrollbar : UnityEngine.MonoBehaviour
    {
        public float value    { get; set; }
        public float size     { get; set; }
        public int   numberOfSteps { get; set; }
        public bool  interactable  { get; set; }
        public class ScrollEvent : UnityEngine.Events.UnityEvent<float> { }
        public ScrollEvent onValueChanged = new ScrollEvent();
    }

    public class ScrollRect : UnityEngine.MonoBehaviour
    {
        public UnityEngine.RectTransform content    { get; set; }
        public UnityEngine.RectTransform viewport   { get; set; }
        public UnityEngine.Vector2       normalizedPosition { get; set; }
        public float  verticalNormalizedPosition   { get; set; }
        public float  horizontalNormalizedPosition { get; set; }
        public bool   horizontal        { get; set; }
        public bool   vertical          { get; set; }
        public MovementType movementType { get; set; }
        public float  elasticity        { get; set; }
        public bool   inertia           { get; set; }
        public float  decelerationRate  { get; set; }
        public float  scrollSensitivity { get; set; }
        public Scrollbar horizontalScrollbar { get; set; }
        public Scrollbar verticalScrollbar   { get; set; }
        public class ScrollRectEvent : UnityEngine.Events.UnityEvent<UnityEngine.Vector2> { }
        public ScrollRectEvent onValueChanged = new ScrollRectEvent();
        public void StopMovement() { }
        public void Rebuild(CanvasUpdate executing) { }
        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() { }
    }
    public enum MovementType { Unrestricted, Elastic, Clamped }

    public class InputField : UnityEngine.MonoBehaviour
    {
        public string    text         { get; set; }
        public bool      readOnly     { get; set; }
        public bool      interactable { get; set; }
        public int       characterLimit { get; set; }
        public ContentType contentType { get; set; }
        public LineType    lineType    { get; set; }
        public int        caretPosition { get; set; }
        public class SubmitEvent : UnityEngine.Events.UnityEvent<string> { }
        public class OnChangeEvent : UnityEngine.Events.UnityEvent<string> { }
        public SubmitEvent  onEndEdit = new SubmitEvent();
        public OnChangeEvent onValueChanged = new OnChangeEvent();
        public void ActivateInputField() { }
        public void DeactivateInputField() { }
        public enum ContentType { Standard, Autocorrected, IntegerNumber, DecimalNumber, Alphanumeric, Name, EmailAddress, Password, Pin, Custom }
        public enum LineType { SingleLine, MultiLineSubmit, MultiLineNewline }
    }

    public class Dropdown : UnityEngine.MonoBehaviour
    {
        public int value { get; set; }
        public class DropdownEvent : UnityEngine.Events.UnityEvent<int> { }
        public DropdownEvent onValueChanged = new DropdownEvent();
        public class OptionData { public string text; public UnityEngine.Sprite image; public OptionData() { } public OptionData(string t) { text = t; } }
        public List<OptionData> options { get; set; } = new List<OptionData>();
        public void AddOptions(List<string> opts) { }
        public void AddOptions(List<OptionData> opts) { }
        public void ClearOptions() { }
        public void RefreshShownValue() { }
        public void Show() { }
        public void Hide() { }
    }

    public class ContentSizeFitter : UnityEngine.MonoBehaviour
    {
        public FitMode horizontalFit { get; set; }
        public FitMode verticalFit   { get; set; }
        public enum FitMode { Unconstrained, MinSize, PreferredSize }
    }

    public class LayoutGroup : UnityEngine.MonoBehaviour
    {
        public RectOffset padding { get; set; }
        public UnityEngine.TextAnchor childAlignment { get; set; }
    }
    public class HorizontalLayoutGroup : LayoutGroup
    {
        public float spacing       { get; set; }
        public bool  childForceExpandWidth  { get; set; }
        public bool  childForceExpandHeight { get; set; }
        public bool  childControlWidth  { get; set; }
        public bool  childControlHeight { get; set; }
    }
    public class VerticalLayoutGroup : LayoutGroup
    {
        public float spacing       { get; set; }
        public bool  childForceExpandWidth  { get; set; }
        public bool  childForceExpandHeight { get; set; }
        public bool  childControlWidth  { get; set; }
        public bool  childControlHeight { get; set; }
    }
    public class GridLayoutGroup : LayoutGroup
    {
        public UnityEngine.Vector2 cellSize  { get; set; }
        public UnityEngine.Vector2 spacing   { get; set; }
        public Axis  startAxis   { get; set; }
        public Corner startCorner { get; set; }
        public Constraint constraint { get; set; }
        public int   constraintCount { get; set; }
        public enum Axis { Horizontal, Vertical }
        public enum Corner { UpperLeft, UpperRight, LowerLeft, LowerRight }
        public enum Constraint { Flexible, FixedColumnCount, FixedRowCount }
    }

    public class LayoutElement : UnityEngine.MonoBehaviour
    {
        public bool  ignoreLayout  { get; set; }
        public float minWidth      { get; set; }
        public float minHeight     { get; set; }
        public float preferredWidth  { get; set; }
        public float preferredHeight { get; set; }
        public float flexibleWidth  { get; set; }
        public float flexibleHeight { get; set; }
        public int   layoutPriority { get; set; }
    }

    public class Mask : UnityEngine.MonoBehaviour { public bool showMaskGraphic { get; set; } }
    public class RectMask2D : UnityEngine.MonoBehaviour { }

    [Serializable] public struct ColorBlock
    {
        public UnityEngine.Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor;
        public float colorMultiplier, fadeDuration;
        public static ColorBlock defaultColorBlock => new ColorBlock();
    }
    [Serializable] public struct SpriteState
    {
        public UnityEngine.Sprite highlightedSprite, pressedSprite, selectedSprite, disabledSprite;
    }

    public static class LayoutUtility
    {
        public static float GetMinWidth(UnityEngine.RectTransform rect) => 0f;
        public static float GetMinHeight(UnityEngine.RectTransform rect) => 0f;
        public static float GetPreferredWidth(UnityEngine.RectTransform rect) => 0f;
        public static float GetPreferredHeight(UnityEngine.RectTransform rect) => 0f;
        public static float GetFlexibleWidth(UnityEngine.RectTransform rect) => 0f;
        public static float GetFlexibleHeight(UnityEngine.RectTransform rect) => 0f;
    }

    public static class RectTransformUtility
    {
        public static bool ScreenPointToLocalPointInRectangle(UnityEngine.RectTransform rect, UnityEngine.Vector2 screenPos, UnityEngine.Camera cam, out UnityEngine.Vector2 localPos) { localPos = UnityEngine.Vector2.zero; return true; }
        public static bool RectangleContainsScreenPoint(UnityEngine.RectTransform rect, UnityEngine.Vector2 screenPos, UnityEngine.Camera cam = null) => true;
        public static bool ScreenPointToWorldPointInRectangle(UnityEngine.RectTransform rect, UnityEngine.Vector2 screenPos, UnityEngine.Camera cam, out UnityEngine.Vector3 worldPos) { worldPos = UnityEngine.Vector3.zero; return true; }
        public static UnityEngine.Vector2 WorldToScreenPoint(UnityEngine.Camera cam, UnityEngine.Vector3 worldPoint) => UnityEngine.Vector2.zero;
        public static UnityEngine.Rect PixelAdjustRect(UnityEngine.RectTransform rectTransform, Canvas canvas) => UnityEngine.Rect.zero;
        public static UnityEngine.Vector3 PixelAdjustPoint(UnityEngine.Vector3 point, UnityEngine.Transform elementTransform, Canvas canvas) => point;
        public static bool IsVisibleFrom(UnityEngine.RectTransform rect, UnityEngine.Camera camera) => true;
    }
} // end UnityEngine.UI

// =============================================================================
//  UnityEngine.EventSystems
// =============================================================================
namespace UnityEngine.EventSystems
{
    public class EventSystem : UnityEngine.MonoBehaviour
    {
        public static EventSystem current { get; set; }
        public bool   sendNavigationEvents { get; set; }
        public int    pixelDragThreshold   { get; set; }
        public bool   isFocused { get; }
        public bool   IsPointerOverGameObject(int pointerId = -1) => false;
        public UnityEngine.GameObject currentSelectedGameObject { get; set; }
        public UnityEngine.GameObject firstSelectedGameObject { get; set; }
        public static bool IsObjectActive(UnityEngine.GameObject obj) => true;
        public void SetSelectedGameObject(UnityEngine.GameObject selected) { }
        public void SetSelectedGameObject(UnityEngine.GameObject selected, BaseEventData pointer) { }
        public void UpdateModules() { }
        public bool IsPointerOverGameObject() => false;
        public UnityEngine.GameObject FindSelectableOnDown() => null;
        public RaycastResult RaycastAll(PointerEventData eventData, List<RaycastResult> resultAppendList) => default;
    }

    public class BaseEventData : System.EventArgs
    {
        public EventSystem currentInputModule { get; }
        public UnityEngine.GameObject selectedObject { get; set; }
        public bool  used { get; }
        public void Use() { }
    }

    public class PointerEventData : BaseEventData
    {
        public UnityEngine.Vector2  position         { get; set; }
        public UnityEngine.Vector2  pressPosition     { get; set; }
        public UnityEngine.Vector2  delta             { get; set; }
        public UnityEngine.Vector2  scrollDelta       { get; set; }
        public bool                 eligibleForClick  { get; set; }
        public int                  pointerId         { get; set; }
        public float                clickTime         { get; set; }
        public int                  clickCount        { get; set; }
        public bool                 dragging          { get; set; }
        public UnityEngine.GameObject pointerEnter    { get; set; }
        public UnityEngine.GameObject pointerPress    { get; set; }
        public UnityEngine.GameObject lastPress       { get; set; }
        public UnityEngine.GameObject pointerDrag     { get; set; }
        public UnityEngine.GameObject pointerClick    { get; set; }
        public RaycastResult         pointerCurrentRaycast { get; set; }
        public RaycastResult         pointerPressRaycast   { get; set; }
        public bool  IsPointerMoving() => false;
        public bool  IsScrolling() => false;
        public UnityEngine.Camera enterEventCamera { get; }
        public UnityEngine.Camera pressEventCamera { get; }
        public InputButton button { get; set; }
        public enum InputButton { Left, Right, Middle }
    }

    public struct RaycastResult
    {
        public UnityEngine.GameObject gameObject { get; set; }
        public float distance, index, depth, sortingLayer, sortingOrder;
        public UnityEngine.Vector3 worldPosition, worldNormal;
        public UnityEngine.Vector2 screenPosition;
        public bool isValid { get; }
        public void Clear() { }
    }

    public class EventTrigger : UnityEngine.MonoBehaviour
    {
        public List<Entry> triggers { get; set; } = new List<Entry>();
        [Serializable] public class Entry { public EventTriggerType eventID; public TriggerEvent callback = new TriggerEvent(); }
        public class TriggerEvent : UnityEngine.Events.UnityEvent<BaseEventData> { }
        public void OnPointerEnter(PointerEventData eventData) { }
        public void OnPointerExit(PointerEventData eventData) { }
        public void OnPointerDown(PointerEventData eventData) { }
        public void OnPointerUp(PointerEventData eventData) { }
        public void OnPointerClick(PointerEventData eventData) { }
        public void OnDrag(PointerEventData eventData) { }
        public void OnDrop(PointerEventData eventData) { }
        public void OnBeginDrag(PointerEventData eventData) { }
        public void OnEndDrag(PointerEventData eventData) { }
    }
    public enum EventTriggerType { PointerEnter=0, PointerExit=1, PointerDown=2, PointerUp=3, PointerClick=4, Drag=5, Drop=6, Scroll=7, UpdateSelected=8, Select=9, Deselect=10, Move=11, InitializePotentialDrag=12, BeginDrag=13, EndDrag=14, Submit=15, Cancel=16 }

    public interface IPointerClickHandler   { void OnPointerClick(PointerEventData d); }
    public interface IPointerDownHandler    { void OnPointerDown(PointerEventData d); }
    public interface IPointerUpHandler      { void OnPointerUp(PointerEventData d); }
    public interface IPointerEnterHandler   { void OnPointerEnter(PointerEventData d); }
    public interface IPointerExitHandler    { void OnPointerExit(PointerEventData d); }
    public interface IDragHandler          { void OnDrag(PointerEventData d); }
    public interface IBeginDragHandler     { void OnBeginDrag(PointerEventData d); }
    public interface IEndDragHandler       { void OnEndDrag(PointerEventData d); }
    public interface IDropHandler          { void OnDrop(PointerEventData d); }
    public interface IScrollHandler        { void OnScroll(PointerEventData d); }
    public interface ISelectHandler        { void OnSelect(BaseEventData d); }
    public interface IDeselectHandler      { void OnDeselect(BaseEventData d); }
    public interface ISubmitHandler        { void OnSubmit(BaseEventData d); }
    public interface ICancelHandler        { void OnCancel(BaseEventData d); }
    public interface IMoveHandler          { void OnMove(AxisEventData d); }
    public interface IUpdateSelectedHandler { void OnUpdateSelected(BaseEventData d); }
    public interface IInitializePotentialDragHandler { void OnInitializePotentialDrag(PointerEventData d); }

    public class AxisEventData : BaseEventData
    {
        public UnityEngine.Vector2 moveVector { get; set; }
        public MoveDirection moveDir { get; set; }
    }
    public enum MoveDirection { Left, Up, Right, Down, None }
    
    public static class ExecuteEvents
    {
        public static bool Execute<T>(UnityEngine.GameObject target, BaseEventData eventData, System.Action<T, BaseEventData> functor) where T : IEventSystemHandler => false;
        public static UnityEngine.GameObject ExecuteHierarchy<T>(UnityEngine.GameObject root, BaseEventData eventData, System.Action<T, BaseEventData> callbackFunction) where T : IEventSystemHandler => null;
        public static bool CanHandleEvent<T>(UnityEngine.GameObject go) where T : IEventSystemHandler => false;
        public static UnityEngine.GameObject GetEventHandler<T>(UnityEngine.GameObject root) where T : IEventSystemHandler => null;
        public static readonly System.Action<IPointerClickHandler, BaseEventData> pointerClickHandler = null;
        public static readonly System.Action<IPointerDownHandler, BaseEventData> pointerDownHandler = null;
        public static readonly System.Action<IPointerUpHandler, BaseEventData> pointerUpHandler = null;
        public static readonly System.Action<IDragHandler, BaseEventData> dragHandler = null;
        public static readonly System.Action<IBeginDragHandler, BaseEventData> beginDragHandler = null;
        public static readonly System.Action<IEndDragHandler, BaseEventData> endDragHandler = null;
        public static readonly System.Action<IDropHandler, BaseEventData> dropHandler = null;
    }
    public interface IEventSystemHandler { }
} // end UnityEngine.EventSystems

// =============================================================================
//  UnityEngine.SceneManagement
// =============================================================================
namespace UnityEngine.SceneManagement
{
    public enum LoadSceneMode { Single, Additive }

    public static class SceneManager
    {
        public static int sceneCount { get; }
        public static int sceneCountInBuildSettings { get; }
        public static UnityEngine.Scene activeScene { get; }
        public static event System.Action<UnityEngine.Scene, LoadSceneMode> sceneLoaded;
        public static event System.Action<UnityEngine.Scene> sceneUnloaded;
        public static event System.Action<UnityEngine.Scene, UnityEngine.Scene> activeSceneChanged;
        public static void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single) { }
        public static void LoadScene(int sceneBuildIndex, LoadSceneMode mode = LoadSceneMode.Single) { }
        public static AsyncOperation LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single) => new AsyncOperation();
        public static AsyncOperation LoadSceneAsync(int sceneBuildIndex, LoadSceneMode mode = LoadSceneMode.Single) => new AsyncOperation();
        public static AsyncOperation UnloadSceneAsync(string sceneName) => new AsyncOperation();
        public static AsyncOperation UnloadSceneAsync(UnityEngine.Scene scene) => new AsyncOperation();
        public static UnityEngine.Scene GetSceneByName(string name) => default;
        public static UnityEngine.Scene GetSceneByPath(string scenePath) => default;
        public static UnityEngine.Scene GetSceneByBuildIndex(int buildIndex) => default;
        public static UnityEngine.Scene GetSceneAt(int index) => default;
        public static bool SetActiveScene(UnityEngine.Scene scene) => false;
        public static void MoveGameObjectToScene(UnityEngine.GameObject go, UnityEngine.Scene scene) { }
        public static void MergeScenes(UnityEngine.Scene sourceScene, UnityEngine.Scene destinationScene) { }
        public static AsyncOperation UnloadSceneAsync(int sceneBuildIndex) => new AsyncOperation();
        public static void CreateScene(string sceneName) { }
    }
} // end UnityEngine.SceneManagement

// =============================================================================
//  UnityEngine.AI  (NavMesh)
// =============================================================================
namespace UnityEngine.AI
{
    public enum NavMeshPathStatus { PathComplete, PathPartial, PathInvalid }
    public enum ObstacleAvoidanceType { NoObstacleAvoidance, LowQualityObstacleAvoidance, MedQualityObstacleAvoidance, GoodQualityObstacleAvoidance, HighQualityObstacleAvoidance }

    public class NavMesh
    {
        public const int AllAreas = -1;
        public static bool SamplePosition(UnityEngine.Vector3 sourcePosition, out NavMeshHit hit, float maxDistance, int areaMask) { hit = default; return false; }
        public static bool Raycast(UnityEngine.Vector3 sourcePosition, UnityEngine.Vector3 targetPosition, out NavMeshHit hit, int areaMask) { hit = default; return false; }
        public static bool FindClosestEdge(UnityEngine.Vector3 sourcePosition, out NavMeshHit hit, int areaMask) { hit = default; return false; }
        public static bool CalculatePath(UnityEngine.Vector3 sourcePosition, UnityEngine.Vector3 targetPosition, int areaMask, NavMeshPath path) => false;
        public static NavMeshDataInstance AddNavMeshData(NavMeshData navMeshData) => default;
        public static void RemoveNavMeshData(NavMeshDataInstance handle) { }
        public static int GetAreaFromName(string areaName) => 0;
        public static string[] GetAreaNames() => new string[0];
        public static int GetSettingsCount() => 0;
        public static event System.Action<NavMeshData> onPreUpdate;
    }

    public struct NavMeshHit { public UnityEngine.Vector3 position, normal; public float distance; public int mask; public bool hit, valid; }
    public class NavMeshPath { public NavMeshPathStatus status { get; } public UnityEngine.Vector3[] corners { get; } public void ClearCorners() { } }
    public class NavMeshData : UnityEngine.Object { }
    public struct NavMeshDataInstance { public bool valid { get; } public UnityEngine.Object owner { get; set; } public void Remove() { } }
    public struct NavMeshBuildSettings { public int agentTypeID; public float agentRadius, agentHeight, agentClimb, agentSlope, minRegionArea, overrideVoxelSize, overrideTileSize; public float voxelSize, tileSize; public bool collectObjects; }
    public enum NavMeshBuildSourceShape { Mesh = 0, Terrain = 1, Box = 2, Sphere = 3, Capsule = 4, ModifierBox = 5 }
    public class NavMeshBuildSource
    {
        public NavMeshBuildSourceShape shape     { get; set; }
        public UnityEngine.Object      sourceData { get; set; }
        public UnityEngine.Matrix4x4   transform  { get; set; }
        public int                     area       { get; set; }
        public int                     generateMeshLinks { get; set; }
    }
    public static class NavMeshBuilder
    {
        public static void UpdateNavMeshData(NavMeshData data, NavMeshBuildSettings buildSettings, System.Collections.Generic.List<NavMeshBuildSource> sources, UnityEngine.Bounds localBounds) { }
        public static System.Collections.IEnumerator UpdateNavMeshDataAsync(NavMeshData data, NavMeshBuildSettings buildSettings, System.Collections.Generic.List<NavMeshBuildSource> sources, UnityEngine.Bounds localBounds) => null;
        public static NavMeshData BuildNavMeshData(NavMeshBuildSettings buildSettings, System.Collections.Generic.List<NavMeshBuildSource> sources, UnityEngine.Bounds localBounds, UnityEngine.Vector3 position, UnityEngine.Quaternion rotation) => null;
        public static void CollectSources(UnityEngine.Bounds worldBounds, int includedLayerMask, NavMeshCollectGeometry geometry, int defaultArea, System.Collections.Generic.List<NavMeshBuildMarkup> markups, System.Collections.Generic.List<NavMeshBuildSource> results) { }
    }
    public class NavMeshBuildMarkup { public bool overrideArea; public int area; public bool ignoreFromBuild; public UnityEngine.Transform root; }

    public class NavMeshAgent : UnityEngine.MonoBehaviour
    {
        public UnityEngine.Vector3 destination { get; set; }
        public float               speed       { get; set; }
        public float               angularSpeed { get; set; }
        public float               acceleration { get; set; }
        public float               stoppingDistance { get; set; }
        public float               radius      { get; set; }
        public float               height      { get; set; }
        public bool                autoBraking { get; set; }
        public bool                autoRepath  { get; set; }
        public bool                updatePosition { get; set; }
        public bool                updateRotation { get; set; }
        public bool                isStopped   { get; set; }
        public bool                hasPath     { get; }
        public bool                pathPending { get; }
        public bool                isOnNavMesh { get; }
        public bool                isOnOffMeshLink { get; }
        public bool                isPathStale { get; }
        public UnityEngine.Vector3 velocity    { get; set; }
        public UnityEngine.Vector3 nextPosition { get; set; }
        public UnityEngine.Vector3 desiredVelocity { get; }
        public NavMeshPath         path        { get; set; }
        public NavMeshPathStatus   pathStatus  { get; }
        public float               remainingDistance { get; }
        public float               baseOffset  { get; set; }
        public int                 areaMask    { get; set; }
        public int                 agentTypeID { get; set; }
        public ObstacleAvoidanceType obstacleAvoidanceType { get; set; }
        public int                 avoidancePriority { get; set; }
        public bool  SetDestination(UnityEngine.Vector3 target) => false;
        public bool  Warp(UnityEngine.Vector3 newPosition) => false;
        public void  Move(UnityEngine.Vector3 offset) { }
        public void  ResetPath() { }
        public bool  CalculatePath(UnityEngine.Vector3 targetPosition, NavMeshPath path) => false;
        public void  ActivateCurrentOffMeshLink(bool activated) { }
        public void  CompleteOffMeshLink() { }
        public void  SetPath(NavMeshPath newPath) { }
        public bool  FindClosestEdge(out NavMeshHit hit) { hit = default; return false; }
        public bool  SamplePathPosition(int areaMask, float maxDistance, out NavMeshHit hit) { hit = default; return false; }
        public bool  Raycast(UnityEngine.Vector3 targetPosition, out NavMeshHit hit) { hit = default; return false; }
    }

    public class NavMeshObstacle : UnityEngine.MonoBehaviour
    {
        public float               radius   { get; set; }
        public float               height   { get; set; }
        public bool                carve    { get; set; }
        public bool                carveOnlyStationary { get; set; }
        public float               carvingMoveThreshold { get; set; }
        public float               carvingTimeToStationary { get; set; }
        public NavMeshObstacleShape shape   { get; set; }
        public UnityEngine.Vector3 center   { get; set; }
        public UnityEngine.Vector3 size     { get; set; }
    }
    public enum NavMeshObstacleShape { Capsule, Box }

    public class NavMeshSurface : UnityEngine.MonoBehaviour
    {
        public int    agentTypeID    { get; set; }
        public NavMeshCollectGeometry collectObjects { get; set; }
        public UnityEngine.LayerMask layerMask  { get; set; }
        public bool   useGeometry   { get; set; }
        public float  overrideTileSize { get; set; }
        public float  overrideVoxelSize { get; set; }
        public bool   buildHeightMesh { get; set; }
        public UnityEngine.Bounds localBounds { get; set; }
        public int    defaultArea   { get; set; }
        public void BuildNavMesh() { }
        public System.Threading.Tasks.Task UpdateNavMesh(NavMeshData data) => System.Threading.Tasks.Task.CompletedTask;
        public void RemoveData() { }
        public void AddData() { }
        public NavMeshData navMeshData { get; set; }
        public NavMeshDataInstance navMeshDataInstance { get; }
    }
    public enum NavMeshCollectGeometry { RenderMeshes, PhysicsColliders }
} // end UnityEngine.AI

// =============================================================================
//  UnityEngine.Events
// =============================================================================
namespace UnityEngine.Events
{
    public class UnityEventBase
    {
        public int GetPersistentEventCount() => 0;
        public string GetPersistentMethodName(int index) => "";
        public UnityEngine.Object GetPersistentTarget(int index) => null;
        public void SetPersistentListenerState(int index, UnityEventCallState state) { }
        public void RemoveAllListeners() { }
    }
    public enum UnityEventCallState { Off, EditorAndRuntime, RuntimeOnly }

    public class UnityEvent : UnityEventBase
    {
        public void AddListener(System.Action call) { }
        public void RemoveListener(System.Action call) { }
        public void Invoke() { }
    }
    public class UnityEvent<T0> : UnityEventBase
    {
        public void AddListener(System.Action<T0> call) { }
        public void RemoveListener(System.Action<T0> call) { }
        public void Invoke(T0 a0) { }
    }
    public class UnityEvent<T0, T1> : UnityEventBase
    {
        public void AddListener(System.Action<T0, T1> call) { }
        public void RemoveListener(System.Action<T0, T1> call) { }
        public void Invoke(T0 a0, T1 a1) { }
    }
    public class UnityEvent<T0, T1, T2> : UnityEventBase
    {
        public void AddListener(System.Action<T0, T1, T2> call) { }
        public void RemoveListener(System.Action<T0, T1, T2> call) { }
        public void Invoke(T0 a0, T1 a1, T2 a2) { }
    }
    public class UnityEvent<T0, T1, T2, T3> : UnityEventBase
    {
        public void AddListener(System.Action<T0, T1, T2, T3> call) { }
        public void RemoveListener(System.Action<T0, T1, T2, T3> call) { }
        public void Invoke(T0 a0, T1 a1, T2 a2, T3 a3) { }
    }
    public delegate void UnityAction();
    public delegate void UnityAction<T0>(T0 a);
    public delegate void UnityAction<T0, T1>(T0 a, T1 b);
} // end UnityEngine.Events

// =============================================================================
//  UnityEngine.Rendering
// =============================================================================
namespace UnityEngine.Rendering
{
    public enum AmbientMode  { Skybox, Trilight, Flat, Custom }
    public enum LightProbeUsage { Off, BlendProbes, UseProxyVolume, CustomProvided }
    public enum ReflectionProbeUsage { Off, BlendProbes, BlendProbesAndSkybox, Simple }
    public enum ShadowSamplingMode { CompareDepths, RawDepth, None }
    public enum ShadowCastingMode  { Off, On, TwoSided, ShadowsOnly }
    public enum VertexAttributeFormat { Float32, Float16, UNorm8, SNorm8, UNorm16, SNorm16, UInt8, SInt8, UInt16, SInt16, UInt32, SInt32 }
    public enum MeshUpdateFlags { Default=0, DontValidateIndices=1, DontResetBoneBounds=2, DontNotifyMeshUsers=4, DontRecalculateBounds=8 }
    public enum IndexFormat { UInt16, UInt32 }
    public enum BlendMode { Zero=0, One=1, DstColor=2, SrcColor=3, OneMinusDstColor=4, SrcAlpha=5, OneMinusSrcColor=6, DstAlpha=7, OneMinusDstAlpha=8, SrcAlphaSaturate=9, OneMinusSrcAlpha=10 }
    public enum CameraEvent { BeforeDepthTexture, AfterDepthTexture, BeforeDepthNormalsTexture, AfterDepthNormalsTexture, BeforeGBuffer, AfterGBuffer, BeforeLighting, AfterLighting, BeforeFinalPass, AfterFinalPass, BeforeForwardAlpha, AfterForwardAlpha, BeforeSkybox, AfterSkybox, BeforeImageEffectsOpaque, AfterImageEffectsOpaque, BeforeReflections, AfterReflections, BeforeHaloAndLensFlares, AfterHaloAndLensFlares, BeforeImageEffects, AfterImageEffects, AfterEverything, BeforeForwardOpaque, AfterForwardOpaque }
    public enum BuiltinRenderTextureType { CurrentActive=-1, CameraTarget=0, Depth=1, DepthNormals=2, ResolvedDepth=3, PrepassNormalsSpec=5, PrepassLight=6, PrepassLightSpec=7, GBuffer0=10, GBuffer1=11, GBuffer2=12, GBuffer3=13, Reflections=14, MotionVectors=15, GBuffer4=16, GBuffer5=17, GBuffer6=18, GBuffer7=19 }

    public class CommandBuffer
    {
        public string name { get; set; }
        public void Clear() { }
        public void DrawMesh(UnityEngine.Mesh mesh, UnityEngine.Matrix4x4 matrix, UnityEngine.Material material, int submeshIndex = 0, int shaderPass = -1) { }
        public void DrawRenderer(UnityEngine.Renderer renderer, UnityEngine.Material material, int submeshIndex = 0, int shaderPass = -1) { }
        public void Blit(UnityEngine.Texture source, UnityEngine.RenderTexture dest) { }
        public void Blit(UnityEngine.Texture source, UnityEngine.RenderTexture dest, UnityEngine.Material mat) { }
        public void Blit(UnityEngine.Texture source, UnityEngine.RenderTexture dest, UnityEngine.Vector2 scale, UnityEngine.Vector2 offset) { }
        public void SetRenderTarget(UnityEngine.RenderTexture rt) { }
        public void ClearRenderTarget(bool clearDepth, bool clearColor, UnityEngine.Color color) { }
        public void GetTemporaryRT(int nameID, int width, int height, int depthBuffer = 0) { }
        public void ReleaseTemporaryRT(int nameID) { }
        public void SetGlobalTexture(string name, UnityEngine.Texture value) { }
        public void SetGlobalFloat(string name, float value) { }
        public void SetGlobalColor(string name, UnityEngine.Color value) { }
        public void SetGlobalVector(string name, UnityEngine.Vector4 value) { }
        public void SetGlobalMatrix(string name, UnityEngine.Matrix4x4 value) { }
        public void EnableShaderKeyword(string keyword) { }
        public void DisableShaderKeyword(string keyword) { }
    }

    public static class GraphicsSettings
    {
        public static UnityEngine.Rendering.RenderPipelineAsset renderPipelineAsset { get; set; }
        public static bool useScriptableRenderPipelineBatching { get; set; }
        public static bool lightsUseLinearIntensity { get; set; }
    }

    public abstract class RenderPipelineAsset : UnityEngine.ScriptableObject { }
    public abstract class RenderPipeline { }
    public class RenderPipelineManager
    {
        public static event System.Action<UnityEngine.Rendering.ScriptableRenderContext, UnityEngine.Camera[]> beginFrameRendering;
        public static event System.Action<UnityEngine.Rendering.ScriptableRenderContext, UnityEngine.Camera> beginCameraRendering;
        public static event System.Action<UnityEngine.Rendering.ScriptableRenderContext, UnityEngine.Camera> endCameraRendering;
        public static event System.Action<UnityEngine.Rendering.ScriptableRenderContext, UnityEngine.Camera[]> endFrameRendering;
    }
    public struct ScriptableRenderContext { public void Submit() { } }
} // end UnityEngine.Rendering

// =============================================================================
//  UnityEngine.InputSystem stubs (New Input System 1.7.0)
// =============================================================================
namespace UnityEngine.InputSystem
{
    public class InputAction
    {
        public string name { get; }
        public bool  enabled { get; }
        public bool  triggered { get; }
        public InputActionPhase phase { get; }
        public struct CallbackContext { public T ReadValue<T>() where T : struct => default; public bool canceled { get; } public bool started { get; } public bool performed { get; } }
        public event System.Action<CallbackContext> started;
        public event System.Action<CallbackContext> performed;
        public event System.Action<CallbackContext> canceled;
        public void Enable() { }
        public void Disable() { }
        public bool  IsPressed() => false;
        public bool  WasPressedThisFrame() => false;
        public bool  WasReleasedThisFrame() => false;
        public T ReadValue<T>() where T : struct => default;
    }
    public enum InputActionPhase { Disabled, Waiting, Started, Performed, Canceled }

    public class InputActionMap
    {
        public string name { get; }
        public bool   enabled { get; }
        public void Enable() { }
        public void Disable() { }
        public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false) => new InputAction();
        public InputAction this[string name] => new InputAction();
    }

    public class InputActionAsset : UnityEngine.ScriptableObject
    {
        public InputActionMap FindActionMap(string nameOrId, bool throwIfNotFound = false) => new InputActionMap();
        public InputAction    FindAction(string actionNameOrId, bool throwIfNotFound = false) => new InputAction();
        public IEnumerable<InputActionMap> actionMaps { get; }
        public void Enable() { }
        public void Disable() { }
    }

    public class PlayerInput : UnityEngine.MonoBehaviour
    {
        public InputActionAsset actions { get; set; }
        public string currentControlScheme { get; }
        public string defaultControlScheme { get; set; }
        public InputActionMap currentActionMap { get; set; }
        public static PlayerInput GetPlayerByIndex(int playerIndex) => null;
        public static PlayerInput FindFirstPairedToDevice(UnityEngine.InputSystem.LowLevel.InputDevice device) => null;
        public InputAction Find(string name) => new InputAction();
    }

    public class Keyboard
    {
        public static Keyboard current { get; }
        public bool   spaceKey  { get; }
        public bool   enterKey  { get; }
        public bool   escapeKey { get; }
        public bool   shiftKey  { get; }
        public bool   ctrlKey   { get; }
        public bool   altKey    { get; }
        public bool   aKey      { get; }
        public bool   wKey      { get; }
        public bool   sKey      { get; }
        public bool   dKey      { get; }
    }

    public class Mouse
    {
        public static Mouse current { get; }
        public bool leftButton   { get; }
        public bool rightButton  { get; }
        public bool middleButton { get; }
        public UnityEngine.Vector2 position { get; }
        public UnityEngine.Vector2 delta    { get; }
        public UnityEngine.Vector2 scroll   { get; }
    }

    public class Gamepad
    {
        public static Gamepad current { get; }
    }
}

namespace UnityEngine.InputSystem.LowLevel
{
    public class InputDevice { }
}

// =============================================================================
//  UnityEditor  (Editor-only)
// =============================================================================
#if UNITY_EDITOR || DOTNET_BUILD_CHECK
namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MenuItem : Attribute
    {
        public string itemName { get; }
        public bool   validate  { get; }
        public int    priority  { get; }
        public MenuItem(string itemName, bool validate = false, int priority = 1000) { this.itemName = itemName; this.validate = validate; this.priority = priority; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class CustomEditor : Attribute { public CustomEditor(Type inspectedType, bool editorForChildClasses = false) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public class CanEditMultipleObjects : Attribute { }

    public class Editor : UnityEngine.ScriptableObject
    {
        public UnityEngine.Object target { get; }
        public UnityEngine.Object[] targets { get; }
        public bool serializedObjectIsModified { get; }
        public virtual void OnInspectorGUI() { }
        public virtual bool HasPreviewGUI() => false;
        public void Repaint() { }
        public static T CreateEditor<T>(UnityEngine.Object targetObject) where T : Editor => null;
    }

    public class EditorGUI
    {
        public static int indentLevel { get; set; }
        public static bool EndChangeCheck() => false;
        public static void BeginChangeCheck() { }
        public static bool Foldout(UnityEngine.Rect pos, bool foldout, string content) => foldout;
        public static bool Foldout(UnityEngine.Rect pos, bool foldout, UnityEngine.GUIContent content) => foldout;
        public static void LabelField(UnityEngine.Rect pos, string label) { }
        public static void LabelField(UnityEngine.Rect pos, string label, string label2) { }
        public static string TextField(UnityEngine.Rect pos, string text) => text;
        public static int IntField(UnityEngine.Rect pos, int val) => val;
        public static float FloatField(UnityEngine.Rect pos, float val) => val;
        public static bool Toggle(UnityEngine.Rect pos, bool val) => val;
        public static UnityEngine.Color ColorField(UnityEngine.Rect pos, UnityEngine.Color val) => val;
        public static void PropertyField(UnityEngine.Rect pos, SerializedProperty prop) { }
        public static void PropertyField(UnityEngine.Rect pos, SerializedProperty prop, bool includeChildren) { }
        public static UnityEngine.Object ObjectField(UnityEngine.Rect pos, UnityEngine.Object obj, Type objType, bool allowSceneObjects) => obj;
        public static int Popup(UnityEngine.Rect pos, int sel, string[] displayedOptions) => sel;
        public static void HelpBox(UnityEngine.Rect pos, string message, MessageType type) { }
        public static void DrawRect(UnityEngine.Rect rect, UnityEngine.Color color) { }
        public static float GetPropertyHeight(SerializedProperty property, bool includeChildren = true) => 0f;
        public static void BeginDisabledGroup(bool disabled) { }
        public static void EndDisabledGroup() { }
        public static void PrefixLabel(UnityEngine.Rect totalPosition, UnityEngine.GUIContent label) { }
    }
    public enum MessageType { None, Info, Warning, Error }

    public class EditorGUILayout
    {
        public static void LabelField(string label, params UnityEngine.GUILayoutOption[] opts) { }
        public static void LabelField(string label, string label2, params UnityEngine.GUILayoutOption[] opts) { }
        public static string TextField(string label, string text, params UnityEngine.GUILayoutOption[] opts) => text;
        public static string TextField(string text, params UnityEngine.GUILayoutOption[] opts) => text;
        public static int IntField(string label, int val, params UnityEngine.GUILayoutOption[] opts) => val;
        public static float FloatField(string label, float val, params UnityEngine.GUILayoutOption[] opts) => val;
        public static float FloatField(float val, params UnityEngine.GUILayoutOption[] opts) => val;
        public static bool Toggle(string label, bool val, params UnityEngine.GUILayoutOption[] opts) => val;
        public static bool Toggle(bool val, params UnityEngine.GUILayoutOption[] opts) => val;
        public static UnityEngine.Color ColorField(string label, UnityEngine.Color val, params UnityEngine.GUILayoutOption[] opts) => val;
        public static int Popup(string label, int sel, string[] displayedOptions, params UnityEngine.GUILayoutOption[] opts) => sel;
        public static int Popup(int sel, string[] displayedOptions, params UnityEngine.GUILayoutOption[] opts) => sel;
        public static UnityEngine.Object ObjectField(string label, UnityEngine.Object obj, Type objType, bool allowSceneObjects, params UnityEngine.GUILayoutOption[] opts) => obj;
        public static void PropertyField(SerializedProperty prop, params UnityEngine.GUILayoutOption[] opts) { }
        public static void PropertyField(SerializedProperty prop, UnityEngine.GUIContent label, params UnityEngine.GUILayoutOption[] opts) { }
        public static void HelpBox(string msg, MessageType type) { }
        public static bool Foldout(bool foldout, string content) => foldout;
        public static bool Foldout(bool foldout, string content, bool toggleOnLabelClick) => foldout;
        public static string TextArea(string text, params UnityEngine.GUILayoutOption[] opts) => text;
        public static void Separator() { }
        public static void Space() { }
        public static void Space(float pixels) { }
        public static bool BeginFoldoutHeaderGroup(bool foldout, string content) => foldout;
        public static void EndFoldoutHeaderGroup() { }
        public static bool DropdownButton(UnityEngine.GUIContent content, FocusType focusType, params UnityEngine.GUILayoutOption[] opts) => false;
        public static string DelayedTextField(string label, string text, params UnityEngine.GUILayoutOption[] opts) => text;
        public static int DelayedIntField(string label, int val, params UnityEngine.GUILayoutOption[] opts) => val;
        public static float DelayedFloatField(string label, float val, params UnityEngine.GUILayoutOption[] opts) => val;
        public static bool InspectorTitlebar(bool foldout, UnityEngine.Object targetObj) => foldout;
        public static float Slider(string label, float val, float min, float max, params UnityEngine.GUILayoutOption[] opts) => val;
        public static float Slider(float val, float min, float max, params UnityEngine.GUILayoutOption[] opts) => val;
        public static int IntSlider(string label, int val, int min, int max, params UnityEngine.GUILayoutOption[] opts) => val;
        public static Vector2Field_Wrapper Vector2Field(string label, UnityEngine.Vector2 val, params UnityEngine.GUILayoutOption[] opts) => new Vector2Field_Wrapper();
        public static Vector3Field_Wrapper Vector3Field(string label, UnityEngine.Vector3 val, params UnityEngine.GUILayoutOption[] opts) => new Vector3Field_Wrapper();
        public static void BeginHorizontal(params UnityEngine.GUILayoutOption[] opts) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params UnityEngine.GUILayoutOption[] opts) { }
        public static void EndVertical() { }
        public static bool BeginToggleGroup(string label, bool toggle) => toggle;
        public static void EndToggleGroup() { }
        public static UnityEngine.Vector2 BeginScrollView(UnityEngine.Vector2 scrollPosition, params UnityEngine.GUILayoutOption[] opts) => scrollPosition;
        public static void EndScrollView() { }
        public static bool PasswordField(string label, string val, params UnityEngine.GUILayoutOption[] opts) => false;
    }
    public struct Vector2Field_Wrapper { public static implicit operator UnityEngine.Vector2(Vector2Field_Wrapper w) => UnityEngine.Vector2.zero; }
    public struct Vector3Field_Wrapper { public static implicit operator UnityEngine.Vector3(Vector3Field_Wrapper w) => UnityEngine.Vector3.zero; }
    public enum FocusType { Passive, Keyboard, Native }

    public static class EditorGUIUtility
    {
        public static float singleLineHeight { get; }
        public static float standardVerticalSpacing { get; }
        public static float labelWidth  { get; set; }
        public static float fieldWidth  { get; set; }
        public static bool  hierarchyMode { get; set; }
        public static bool  wideMode     { get; set; }
        public static UnityEngine.GUIContent TrTextContent(string text, string tooltip = null, UnityEngine.Texture icon = null) => new UnityEngine.GUIContent(text);
        public static UnityEngine.GUIContent IconContent(string name, string tooltip = "") => new UnityEngine.GUIContent();
        public static UnityEngine.Texture2D FindTexture(string name) => null;
        public static UnityEngine.Texture2D GetIconForObject(UnityEngine.Object obj) => null;
        public static void SetIconForObject(UnityEngine.Object obj, UnityEngine.Texture2D icon) { }
        public static UnityEngine.Object LoadRequired(string path) => null;
        public static float GetBuiltinExtraSettingsEventListHeight(float height) => 0f;
        public static void PingObject(int targetInstanceID) { }
        public static void PingObject(UnityEngine.Object targetObject) { }
    }

    public static class EditorUtility
    {
        public static bool  IsPersistent(UnityEngine.Object obj) => false;
        public static void  SetDirty(UnityEngine.Object target) { }
        public static void  ClearProgressBar() { }
        public static void  DisplayProgressBar(string title, string info, float progress) { }
        public static bool  DisplayDialog(string title, string message, string ok, string cancel = "") => true;
        public static int   DisplayDialogComplex(string title, string message, string ok, string cancel, string alt) => 0;
        public static void  DisplayCancelableProgressBar(string title, string info, float progress) { }
        public static bool  IsDirty(UnityEngine.Object target) => false;
        public static string SaveFilePanelInProject(string title, string defaultName, string extension, string message, string path = "") => "";
        public static string SaveFilePanel(string title, string directory, string defaultName, string extension) => "";
        public static string SaveFolderPanel(string title, string folder, string defaultName) => "";
        public static string OpenFilePanel(string title, string directory, string extension) => "";
        public static string OpenFolderPanel(string title, string folder, string defaultName) => "";
        public static string[] OpenFilePanelWithFilters(string title, string directory, string[] filters) => new string[0];
        public static string[] OpenFilePanelMultiSelect(string title, string directory, string extension) => new string[0];
        public static void  RevealInFinder(string path) { }
        public static void  OpenWithDefaultApp(string path) { }
        public static bool  CopySerialized(UnityEngine.Object source, UnityEngine.Object dest) => false;
        public static T[]   CollectDeepHierarchy<T>(UnityEngine.Object[] roots) where T : UnityEngine.Object => new T[0];
        public static void  FocusProjectWindow() { }
    }

    public static class AssetDatabase
    {
        public static string GetAssetPath(UnityEngine.Object assetObject) => "";
        public static string GetAssetPath(int instanceID) => "";
        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object => null;
        public static UnityEngine.Object LoadAssetAtPath(string assetPath, Type type) => null;
        public static string[] GetAllAssetPaths() => new string[0];
        public static string[] FindAssets(string filter, string[] searchInFolders = null) => new string[0];
        public static string GUIDToAssetPath(string guid) => "";
        public static string AssetPathToGUID(string path) => "";
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static void SaveAssets() { }
        public static void Refresh() { }
        public static void Refresh(ImportAssetOptions options) { }
        public static bool MoveAsset(string oldPath, string newPath) => true;
        public static bool DeleteAsset(string path) => true;
        public static bool CopyAsset(string path, string newPath) => true;
        public static void ImportAsset(string path) { }
        public static void ImportAsset(string path, ImportAssetOptions options) { }
        public static bool IsValidFolder(string path) => false;
        public static string CreateFolder(string parentFolder, string newFolderName) => "";
        public static string[] GetSubFolders(string path) => new string[0];
        public static bool IsOpenForEdit(string assetOrMetaFilePath) => true;
        public static string GenerateUniqueAssetPath(string path) => path;
        public static void AddObjectToAsset(UnityEngine.Object objectToAdd, UnityEngine.Object assetObject) { }
        public static void AddObjectToAsset(UnityEngine.Object objectToAdd, string path) { }
        public static void SetMainObject(UnityEngine.Object mainObject, string assetPath) { }
        public static bool Contains(UnityEngine.Object obj) => false;
        public static bool Contains(int instanceID) => false;
    }
    public enum ImportAssetOptions { Default=0, ForceUpdate=1, ForceSynchronousImport=8, ImportRecursive=256, DontDownloadFromCacheServer=32768, TwoPass=131072 }

    public static class Selection
    {
        public static UnityEngine.Object activeObject { get; set; }
        public static UnityEngine.GameObject activeGameObject { get; set; }
        public static UnityEngine.Transform activeTransform { get; set; }
        public static int activeInstanceID { get; set; }
        public static UnityEngine.Object[] objects { get; set; }
        public static UnityEngine.GameObject[] gameObjects { get; set; }
        public static UnityEngine.Transform[] transforms { get; set; }
        public static T[] GetFiltered<T>(SelectionMode mode) where T : UnityEngine.Object => new T[0];
        public static bool Contains(int instanceID) => false;
        public static bool Contains(UnityEngine.Object obj) => false;
    }
    public enum SelectionMode { Unfiltered=0, TopLevel=1, Deep=2, ExcludePrefab=8, Editable=16, Assets=32, DeepAssets=64 }

    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object obj) { }
        public SerializedObject(UnityEngine.Object[] objs) { }
        public UnityEngine.Object targetObject { get; }
        public UnityEngine.Object[] targetObjects { get; }
        public bool isEditingMultipleObjects { get; }
        public bool hasModifiedProperties { get; }
        public SerializedProperty FindProperty(string propertyPath) => new SerializedProperty();
        public SerializedProperty GetIterator() => new SerializedProperty();
        public void Update() { }
        public void UpdateIfRequiredOrScript() { }
        public bool ApplyModifiedProperties() => false;
        public bool ApplyModifiedPropertiesWithoutUndo() => false;
        public void SetIsDifferentCacheDirty() { }
    }

    public class SerializedProperty
    {
        public string name        { get; }
        public string displayName { get; }
        public string tooltip     { get; }
        public string propertyPath { get; }
        public SerializedPropertyType propertyType { get; }
        public bool   boolValue     { get; set; }
        public int    intValue      { get; set; }
        public float  floatValue    { get; set; }
        public string stringValue   { get; set; }
        public UnityEngine.Color colorValue { get; set; }
        public UnityEngine.Object objectReferenceValue { get; set; }
        public int    enumValueIndex { get; set; }
        public string[] enumNames   { get; }
        public string[] enumDisplayNames { get; }
        public UnityEngine.Vector2 vector2Value { get; set; }
        public UnityEngine.Vector3 vector3Value { get; set; }
        public UnityEngine.Vector4 vector4Value { get; set; }
        public UnityEngine.Rect rectValue { get; set; }
        public int    arraySize      { get; set; }
        public bool   isArray        { get; }
        public bool   isExpanded     { get; set; }
        public bool   hasChildren    { get; }
        public bool   hasVisibleChildren { get; }
        public bool   isAnimated     { get; }
        public bool   editable       { get; }
        public bool   hasMultipleDifferentValues { get; }
        public int    depth          { get; }
        public SerializedObject serializedObject { get; }
        public SerializedProperty FindPropertyRelative(string relativePropertyPath) => new SerializedProperty();
        public SerializedProperty GetArrayElementAtIndex(int index) => new SerializedProperty();
        public void   InsertArrayElementAtIndex(int index) { }
        public void   DeleteArrayElementAtIndex(int index) { }
        public void   MoveArrayElement(int srcIndex, int dstIndex) { }
        public bool   NextVisible(bool enterChildren) => false;
        public bool   Next(bool enterChildren) => false;
        public void   Reset() { }
        public SerializedProperty Copy() => new SerializedProperty();
    }
    public enum SerializedPropertyType { Generic=-1, Integer=0, Boolean=1, Float=2, String=3, Color=4, ObjectReference=5, LayerMask=6, Enum=7, Vector2=8, Vector3=9, Vector4=10, Rect=11, ArraySize=12, Character=13, AnimationCurve=14, Bounds=15, Gradient=16, Quaternion=17, ExposedReference=18, FixedBufferSize=19, Vector2Int=20, Vector3Int=21, RectInt=22, BoundsInt=23, ManagedReference=24 }

    public class PropertyDrawer
    {
        public virtual UnityEngine.GUIContent label { get; }
        public virtual float GetPropertyHeight(SerializedProperty property, UnityEngine.GUIContent label) => EditorGUIUtility.singleLineHeight;
        public virtual void OnGUI(UnityEngine.Rect position, SerializedProperty property, UnityEngine.GUIContent label) { }
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class CustomPropertyDrawer : Attribute { public CustomPropertyDrawer(Type type, bool useForChildren = false) { } }

    public class EditorWindow : UnityEngine.ScriptableObject
    {
        public string title { get; set; }
        public UnityEngine.Rect position { get; set; }
        public bool   hasFocus { get; }
        public bool   docked { get; }
        public bool   wantsMouseMove { get; set; }
        public bool   autoRepaintOnSceneChange { get; set; }
        public bool   maximized { get; set; }
        public float  minSize_x { get; set; }
        public float  minSize_y { get; set; }
        public UnityEngine.Vector2 minSize { get; set; }
        public UnityEngine.Vector2 maxSize { get; set; }
        public static T GetWindow<T>(bool utility = false, string title = null, bool focus = true) where T : EditorWindow => Activator.CreateInstance<T>();
        public static T GetWindow<T>(string title, bool utility, bool focus = true) where T : EditorWindow => Activator.CreateInstance<T>();
        public static T CreateInstance<T>() where T : EditorWindow => Activator.CreateInstance<T>();
        public void Repaint() { }
        public void Close() { }
        public void Focus() { }
        public void Show() { }
        public void ShowUtility() { }
        public void ShowPopup() { }
        public void ShowAuxWindow() { }
        public void ShowModal() { }
        public void SaveChanges() { }
        public bool HasUnsavedChanges() => false;
        public virtual void OnGUI() { }
        public virtual void OnEnable() { }
        public virtual void OnDisable() { }
        public virtual void OnFocus() { }
        public virtual void OnLostFocus() { }
        public virtual void OnInspectorUpdate() { }
        public virtual void OnDestroy() { }
    }

    public class SceneView : EditorWindow
    {
        public static SceneView lastActiveSceneView { get; }
        public static SceneView currentDrawingSceneView { get; }
        public UnityEngine.Camera camera { get; }
        public bool   in2DMode { get; set; }
        public static void RepaintAll() { }
        public static void FrameLastActiveSceneView() { }
        public void Frame(UnityEngine.Bounds bounds, bool instant = false) { }
        public void LookAt(UnityEngine.Vector3 point) { }
        public static event System.Action<SceneView> duringSceneGui;
        public static event System.Action<SceneView> beforeSceneGui;
    }

    public static class Undo
    {
        public static void RecordObject(UnityEngine.Object objectToUndo, string name) { }
        public static void RecordObjects(UnityEngine.Object[] objectsToUndo, string name) { }
        public static void RegisterCreatedObjectUndo(UnityEngine.Object objectToUndo, string name) { }
        public static void DestroyObjectImmediate(UnityEngine.Object objectToUndo) { }
        public static void RegisterCompleteObjectUndo(UnityEngine.Object objectToUndo, string name) { }
        public static void SetTransformParent(UnityEngine.Transform t, UnityEngine.Transform parent, string name) { }
        public static void AddComponent<T>(UnityEngine.GameObject go) where T : UnityEngine.Component { }
        public static void CollapseUndoOperations(int group) { }
        public static void IncrementCurrentGroup() { }
        public static int  GetCurrentGroup() => 0;
        public static void SetCurrentGroupName(string name) { }
        public static void PerformUndo() { }
        public static void PerformRedo() { }
        public static void ClearAll() { }
        public static event System.Action undoRedoPerformed;
    }

    public static class PrefabUtility
    {
        public static bool IsPartOfPrefabAsset(UnityEngine.Object obj) => false;
        public static bool IsPartOfPrefabInstance(UnityEngine.Object obj) => false;
        public static bool IsPartOfAnyPrefab(UnityEngine.Object obj) => false;
        public static bool IsPrefabAssetMissing(UnityEngine.Object instanceComponentOrGameObject) => false;
        public static UnityEngine.GameObject GetCorrespondingObjectFromOriginalSource<T>(T componentOrGameObject) where T : UnityEngine.Object => null;
        public static UnityEngine.Object GetCorrespondingObjectFromOriginalSource(UnityEngine.Object obj) => null;
        public static UnityEngine.GameObject GetNearestPrefabInstanceRoot(UnityEngine.Object componentOrGameObject) => null;
        public static UnityEngine.GameObject LoadPrefabContents(string assetPath) => null;
        public static void SaveAsPrefabAsset(UnityEngine.GameObject go, string assetPath) { }
        public static bool SaveAsPrefabAssetAndConnect(UnityEngine.GameObject go, string assetPath, InteractionMode action) => false;
        public static void UnloadPrefabContents(UnityEngine.GameObject contentsRoot) { }
        public static UnityEngine.GameObject InstantiatePrefab(UnityEngine.Object assetComponentOrGameObject) => new UnityEngine.GameObject();
        public static UnityEngine.GameObject InstantiatePrefab(UnityEngine.Object assetComponentOrGameObject, UnityEngine.Transform parent) => new UnityEngine.GameObject();
        public static void ApplyPrefabInstance(UnityEngine.GameObject instanceRoot, InteractionMode action) { }
        public static void RevertPrefabInstance(UnityEngine.GameObject instanceRoot, InteractionMode action) { }
        public static PropertyModification[] GetPropertyModifications(UnityEngine.Object targetPrefab) => new PropertyModification[0];
        public static void SetPropertyModifications(UnityEngine.Object targetPrefab, PropertyModification[] modifications) { }
        public static bool HasPrefabInstanceAnyOverrides(UnityEngine.GameObject instanceRoot, bool includeDefaultOverrides) => false;
    }
    public enum InteractionMode { AutomatedAction, UserAction }
    public class PropertyModification { public UnityEngine.Object target; public string propertyPath; public string value; public UnityEngine.Object objectReference; }

    public class Handles
    {
        public static UnityEngine.Color color { get; set; }
        public static UnityEngine.Matrix4x4 matrix { get; set; }
        public static void DrawLine(UnityEngine.Vector3 p1, UnityEngine.Vector3 p2) { }
        public static void DrawLines(UnityEngine.Vector3[] lineSegments) { }
        public static void DrawPolyLine(params UnityEngine.Vector3[] points) { }
        public static void DrawWireDisc(UnityEngine.Vector3 center, UnityEngine.Vector3 normal, float radius) { }
        public static void DrawWireCube(UnityEngine.Vector3 center, UnityEngine.Vector3 size) { }
        public static void DrawSolidDisc(UnityEngine.Vector3 center, UnityEngine.Vector3 normal, float radius) { }
        public static void DrawSolidRectangleWithOutline(UnityEngine.Vector3[] verts, UnityEngine.Color faceColor, UnityEngine.Color outlineColor) { }
        public static UnityEngine.Vector3 PositionHandle(UnityEngine.Vector3 position, UnityEngine.Quaternion rotation) => position;
        public static UnityEngine.Quaternion RotationHandle(UnityEngine.Quaternion rotation, UnityEngine.Vector3 position) => rotation;
        public static UnityEngine.Vector3 ScaleHandle(UnityEngine.Vector3 scale, UnityEngine.Vector3 position, UnityEngine.Quaternion rotation, float size) => scale;
        public static bool Button(UnityEngine.Vector3 position, UnityEngine.Quaternion direction, float size, float pickSize, CapFunction capFunction) => false;
        public delegate void CapFunction(int controlID, UnityEngine.Vector3 position, UnityEngine.Quaternion rotation, float size, EventType eventType);
        public static float SphereHandleSize(UnityEngine.Vector3 position) => 0f;
        public static float GetMainGameViewSize_x() => Screen.width;
        public static void Label(UnityEngine.Vector3 position, string text) { }
        public static void Label(UnityEngine.Vector3 position, UnityEngine.GUIContent content) { }
        public static void BeginGUI() { }
        public static void EndGUI() { }
    }
    public enum EventType { MouseDown=0, MouseUp=1, MouseMove=2, MouseDrag=3, KeyDown=4, KeyUp=5, ScrollWheel=6, Repaint=7, Layout=8, DragUpdated=9, DragPerform=10, DragExited=15, Ignore=11, Used=12, ValidateCommand=13, ExecuteCommand=14, TouchDown=30, TouchUp=31, TouchMove=32, TouchEnter=33, TouchLeave=34, TouchStationary=35 }
} // end UnityEditor

namespace UnityEditor.SceneManagement
{
    public static class EditorSceneManager
    {
        public static UnityEngine.Scene NewScene(NewSceneSetup setup, NewSceneMode mode = NewSceneMode.Single) => default;
        public static UnityEngine.Scene OpenScene(string scenePath, OpenSceneMode mode = OpenSceneMode.Single) => default;
        public static bool SaveScene(UnityEngine.Scene scene, string dstScenePath = "", bool saveAsCopy = false) => true;
        public static bool SaveCurrentModifiedScenesIfUserWantsTo() => true;
        public static bool SaveOpenScenes() => true;
        public static UnityEngine.Scene GetSceneByName(string name) => default;
        public static UnityEngine.Scene GetSceneAt(int index) => default;
        public static int sceneCount { get; }
        public static int loadedSceneCount { get; }
        public static event System.Action<UnityEngine.Scene, OpenSceneMode> sceneOpened;
        public static event System.Action<UnityEngine.Scene> sceneClosed;
        public static event System.Action<UnityEngine.Scene, UnityEngine.Scene> activeSceneChangedInEditMode;
        public static void CloseScene(UnityEngine.Scene scene, bool removeScene) { }
        public static UnityEngine.Scene GetActiveScene() => default;
        public static bool SetActiveScene(UnityEngine.Scene scene) => false;
        public static void MoveGameObjectToScene(UnityEngine.GameObject go, UnityEngine.Scene scene) { }
        public static void MarkSceneDirty(UnityEngine.Scene scene) { }
        public static void MoveSceneBefore(UnityEngine.Scene src, UnityEngine.Scene dst) { }
        public static void MoveSceneAfter(UnityEngine.Scene src, UnityEngine.Scene dst) { }
        public static System.Threading.Tasks.Task<bool> SaveModifiedScenesIfUserWantsToAsync(UnityEngine.Scene[] scenes) => System.Threading.Tasks.Task.FromResult(true);
    }
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode  { Single, Additive }
    public enum OpenSceneMode { Single, Additive, AdditiveWithoutLoading }
}

namespace UnityEditor.Animations
{
    public class AnimatorController : UnityEngine.RuntimeAnimatorController
    {
        public AnimatorControllerLayer[] layers { get; set; }
        public UnityEngine.AnimatorControllerParameter[] parameters { get; set; }
        public static AnimatorController CreateAnimatorControllerAtPath(string path) => null;
        public void AddLayer(string name) { }
        public void AddLayer(AnimatorControllerLayer layer) { }
        public void RemoveLayer(int index) { }
        public void AddParameter(string name, UnityEngine.AnimatorControllerParameterType type) { }
        public void AddParameter(UnityEngine.AnimatorControllerParameter parameter) { }
        public void RemoveParameter(int index) { }
        public AnimatorStateMachine GetStateMachine(int layerIndex) => null;
    }
    public class AnimatorControllerLayer
    {
        public string name { get; set; }
        public float  defaultWeight { get; set; }
        public AnimatorStateMachine stateMachine { get; set; }
        public UnityEngine.AvatarMask avatarMask { get; set; }
        public UnityEngine.AnimationBlendMode blendingMode { get; set; }
        public bool   iKPass { get; set; }
    }
    public class AnimatorStateMachine : UnityEngine.Object
    {
        public AnimatorState[] states { get; }
        public AnimatorState defaultState { get; set; }
        public AnimatorState AddState(string name) => null;
        public void RemoveState(AnimatorState state) { }
        public AnimatorStateTransition AddAnyStateTransition(AnimatorState destinationState) => null;
        public AnimatorStateTransition AddEntryTransition(AnimatorState destinationState) => null;
    }
    public class AnimatorState : UnityEngine.Object
    {
        public string name { get; set; }
        public UnityEngine.Motion motion { get; set; }
        public float  speed { get; set; }
        public int    tag { get; set; }
        public bool   writeDefaultValues { get; set; }
        public AnimatorStateTransition[] transitions { get; }
        public AnimatorStateTransition AddTransition(AnimatorState destinationState) => null;
        public AnimatorStateTransition AddTransition(AnimatorState destinationState, bool defaultExitTime) => null;
    }
    public class AnimatorStateTransition : UnityEngine.Object
    {
        public bool  hasExitTime { get; set; }
        public float exitTime { get; set; }
        public float duration { get; set; }
        public bool  hasFixedDuration { get; set; }
        public bool  canTransitionToSelf { get; set; }
        public bool  orderedInterruption { get; set; }
        public AnimatorCondition[] conditions { get; }
        public void AddCondition(AnimatorConditionMode mode, float threshold, string parameter) { }
    }
    public struct AnimatorCondition { public AnimatorConditionMode mode; public string parameter; public float threshold; }
    public enum AnimatorConditionMode { If, IfNot, Greater, Less, Equals, NotEqual }
}
public class Motion : UnityEngine.Object { }
public class AvatarMask : UnityEngine.Object { }
#endif // UNITY_EDITOR

