using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class FFTOcean : MonoBehaviour
{
    //computeShader
    public ComputeShader OceanCS;

    //变量
    public float A = 10;
    public int fftPower = 9;
    public Vector4 Wind = new Vector4(1, 0, 0, 0);
    public float WindScale = 30;
    public float TimeScale = 1;
    public float HeightScale = 1;
    public float AlphaScale = 1;
    public int MeshSize = 256;		//海面大小
    public float MeshLength = 100;	//海面长度
    public float BubblesScale = 1.0f;
    public float BubblesThreshold = 0.6f;

    private int fftSize;
    private float time = 0;

    private int[] vertIndexs;		//点索引
    private Vector3[] positions;    //点
    private Vector2[] uvs; 			//uv坐标
    private Mesh mesh;
    private MeshFilter filetr;
    private MeshRenderer render;


    //material
    public Material DisplaceYMat;
    public Material DisplaceXMat;
    public Material DisplaceZMat;
    public Material OceanMaterial;
    public Material NormalMaterial;
    public Material BubblesMaterial;

    //核函数编号
    private int KernelComputeGaussianRandom;
    private int KernelComputeHeight;
    private int KernelComputeDisplaceXZ;
    private int KernelFFT_1_Init;
    private int KernelFFT_1;
    private int KernelFFT_2_Init;
    private int KernelFFT_2;
    private int KernelTextureGenerationDisplace;
    private int KernelTextureGenerationNormalBubbles;

    //材质
    private RenderTexture GaussianRandomRT;
    private RenderTexture DisplaceYRT;
    private RenderTexture DisplaceXRT;
    private RenderTexture DisplaceZRT;
    private RenderTexture DisplaceRT;
    private RenderTexture NormalRT;
    private RenderTexture BubblesRT;
    private RenderTexture TempRT;

    private void Awake()
    {
        //构建海面模型
        filetr = gameObject.GetComponent<MeshFilter>();
        if (filetr == null)
        {
            filetr = gameObject.AddComponent<MeshFilter>();
        }
        render = gameObject.GetComponent<MeshRenderer>();
        if (render == null)
        {
            render = gameObject.AddComponent<MeshRenderer>();
        }
        mesh = new Mesh();
        filetr.mesh = mesh;
        render.material = OceanMaterial;
    }

    void Start()
    {
        CreateMesh();
        InitlizeValue();
    }

    void Update()
    {
        //更新时间
        time += Time.deltaTime * TimeScale;
        ComputeOcean();
        SetTexture();
    }
    void CreateMesh()
    {
        vertIndexs = new int[(MeshSize - 1) * (MeshSize - 1) * 6];
        positions = new Vector3[MeshSize * MeshSize];
        uvs = new Vector2[MeshSize * MeshSize];

        int inx = 0;
        for (int i = 0; i < MeshSize; i++)
        {
            for (int j = 0; j < MeshSize; j++)
            {
                int index = i * MeshSize + j;
                positions[index] = new Vector3((j - MeshSize / 2.0f) * MeshLength / MeshSize, 0, (i - MeshSize / 2.0f) * MeshLength / MeshSize);
                uvs[index] = new Vector2(j / (MeshSize - 1.0f), i / (MeshSize - 1.0f));

                //当点不处于边界时
                if (i != MeshSize - 1 && j != MeshSize - 1)
                {
                    vertIndexs[inx++] = index;
                    vertIndexs[inx++] = index + MeshSize;
                    vertIndexs[inx++] = index + MeshSize + 1;

                    vertIndexs[inx++] = index;
                    vertIndexs[inx++] = index + MeshSize + 1;
                    vertIndexs[inx++] = index + 1;
                }
            }
        }
        mesh.vertices = positions;
        mesh.SetIndices(vertIndexs, MeshTopology.Triangles, 0);
        mesh.uv = uvs;
    }
    void InitlizeValue()
    {
        fftSize = (int)Mathf.Pow(2, fftPower);

        //获取所有核函数ID
        KernelComputeGaussianRandom = OceanCS.FindKernel("ComputeGaussianRandom");
        KernelComputeHeight = OceanCS.FindKernel("ComputeHeight");
        KernelFFT_1_Init = OceanCS.FindKernel("FFT_1_Init");
        KernelFFT_1 = OceanCS.FindKernel("FFT_1");
        KernelFFT_2_Init = OceanCS.FindKernel("FFT_2_Init");
        KernelFFT_2 = OceanCS.FindKernel("FFT_2");
        KernelTextureGenerationDisplace = OceanCS.FindKernel("TextureGenerationDisplace");
        KernelComputeDisplaceXZ = OceanCS.FindKernel("ComputeDisplaceXZ");
        KernelTextureGenerationNormalBubbles = OceanCS.FindKernel("TextureGenerationNormalBubbles");

        //获取材质
        if (GaussianRandomRT != null && GaussianRandomRT.IsCreated())
        {
            GaussianRandomRT.Release();
            DisplaceYRT.Release();
            DisplaceXRT.Release();
            DisplaceZRT.Release();
            TempRT.Release();
            DisplaceRT.Release();
            NormalRT.Release();
            BubblesRT.Release();
        }
        GaussianRandomRT = CreateRT(fftSize);
        DisplaceYRT = CreateRT(fftSize);
        DisplaceXRT = CreateRT(fftSize);
        DisplaceZRT = CreateRT(fftSize);
        TempRT = CreateRT(fftSize);
        DisplaceRT = CreateRT(fftSize);
        NormalRT = CreateRT(fftSize);
        BubblesRT = CreateRT(fftSize);

        //设置变量
        OceanCS.SetInt("N", fftSize);
        OceanCS.SetInt("fftPower", fftPower);

        //获取高斯随机数
        OceanCS.SetTexture(KernelComputeGaussianRandom, "GaussianRandomRT", GaussianRandomRT);
        OceanCS.Dispatch(KernelComputeGaussianRandom, fftSize / 8, fftSize / 8, 1);
    }
    void ComputeOcean()
    {
        //设置变量
        fftSize = (int)Mathf.Pow(2, fftPower);
        OceanCS.SetInt("N", fftSize);
        OceanCS.SetFloat("OceanLength", MeshLength);
        OceanCS.SetInt("fftPower", fftPower);
        OceanCS.SetFloat("HeightScale", HeightScale);
        OceanCS.SetFloat("AlphaScale", AlphaScale);
        OceanCS.SetFloat("BubblesScale", BubblesScale);
        OceanCS.SetFloat("BubblesThreshold", BubblesThreshold);

        OceanCS.SetFloat("A", A);
        OceanCS.SetFloat("Time", time);
        //获取风的随机因子
        Wind.z = Random.Range(1f, 10f);
        Wind.w = Random.Range(1f, 10f);
        Vector2 w = new Vector2(Wind.x, Wind.y);
        w.Normalize();
        w *= WindScale;
        OceanCS.SetVector("wind", new Vector4(w.x, w.y, Wind.z, Wind.w));


        //获取高度频谱
        OceanCS.SetTexture(KernelComputeHeight, "GaussianRandomRT", GaussianRandomRT);
        OceanCS.SetTexture(KernelComputeHeight, "DisplaceYRT", DisplaceYRT);
        OceanCS.Dispatch(KernelComputeHeight, fftSize / 8, fftSize / 8, 1);
        //获取XZ偏移
        OceanCS.SetTexture(KernelComputeDisplaceXZ, "DisplaceYRT", DisplaceYRT);
        OceanCS.SetTexture(KernelComputeDisplaceXZ, "DisplaceXRT", DisplaceXRT);
        OceanCS.SetTexture(KernelComputeDisplaceXZ, "DisplaceZRT", DisplaceZRT);
        OceanCS.Dispatch(KernelComputeDisplaceXZ, fftSize / 8, fftSize / 8, 1);

        //横向IFFT
        //第一次单独处理
        ComputeFFT(KernelFFT_1_Init, ref DisplaceYRT, ref TempRT);
        ComputeFFT(KernelFFT_1_Init, ref DisplaceXRT, ref TempRT);
        ComputeFFT(KernelFFT_1_Init, ref DisplaceZRT, ref TempRT);
        for (int i = 1; i <= fftPower; i++)
        {
            int sn = (1 << i);
            OceanCS.SetInt("SN", sn);
            ComputeFFT(KernelFFT_1, ref DisplaceYRT, ref TempRT);
            ComputeFFT(KernelFFT_1, ref DisplaceXRT, ref TempRT);
            ComputeFFT(KernelFFT_1, ref DisplaceZRT, ref TempRT);
        }
        //return;

        //纵向IFFT
        //第一次单独处理
        ComputeFFT(KernelFFT_2_Init, ref DisplaceYRT, ref TempRT);
        ComputeFFT(KernelFFT_2_Init, ref DisplaceXRT, ref TempRT);
        ComputeFFT(KernelFFT_2_Init, ref DisplaceZRT, ref TempRT);
        //return;
        for (int i = 1; i <= fftPower; i++)
        {
            int sn = (1 << i);
            OceanCS.SetInt("SN", sn);
            ComputeFFT(KernelFFT_2, ref DisplaceYRT, ref TempRT);
            ComputeFFT(KernelFFT_2, ref DisplaceXRT, ref TempRT);
            ComputeFFT(KernelFFT_2, ref DisplaceZRT, ref TempRT);
        }
        //将频谱的模长变为高度
        OceanCS.SetTexture(KernelTextureGenerationDisplace, "DisplaceYRT", DisplaceYRT);
        OceanCS.SetTexture(KernelTextureGenerationDisplace, "DisplaceXRT", DisplaceXRT);
        OceanCS.SetTexture(KernelTextureGenerationDisplace, "DisplaceZRT", DisplaceZRT);
        OceanCS.SetTexture(KernelTextureGenerationDisplace, "DisplaceRT", DisplaceRT);
        OceanCS.Dispatch(KernelTextureGenerationDisplace, fftSize / 8, fftSize / 8, 1);

        //获取法线和泡沫
        OceanCS.SetTexture(KernelTextureGenerationNormalBubbles, "DisplaceRT", DisplaceRT);
        OceanCS.SetTexture(KernelTextureGenerationNormalBubbles, "NormalRT", NormalRT);
        OceanCS.SetTexture(KernelTextureGenerationNormalBubbles, "BubblesRT", BubblesRT);
        OceanCS.Dispatch(KernelTextureGenerationNormalBubbles, fftSize / 8, fftSize / 8, 1);
    }

    void ComputeFFT(int kernel, ref RenderTexture InputRT, ref RenderTexture OutputRT)
    {
        OceanCS.SetTexture(kernel, "InputRT", InputRT);
        OceanCS.SetTexture(kernel, "OutputRT", OutputRT);
        OceanCS.Dispatch(kernel, fftSize / 8, fftSize / 8, 1);
        //交换输入输出
        RenderTexture rt = InputRT;
        InputRT = OutputRT;
        OutputRT = rt;
    }
    private RenderTexture CreateRT(int size)
    {
        RenderTexture rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }
    private void SetTexture()
    {
        OceanMaterial.SetTexture("_Displace", DisplaceRT);
        OceanMaterial.SetTexture("_Normal", NormalRT);
        OceanMaterial.SetTexture("_Bubbles", BubblesRT);

        NormalMaterial.SetTexture("_MainTex", NormalRT);
        BubblesMaterial.SetTexture("_MainTex", BubblesRT);
        DisplaceYMat.SetTexture("_MainTex", DisplaceYRT);
        DisplaceXMat.SetTexture("_MainTex", DisplaceXRT);
        DisplaceZMat.SetTexture("_MainTex", DisplaceZRT);
    }
}
