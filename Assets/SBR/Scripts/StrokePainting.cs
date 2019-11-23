using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
[Serializable]
[PostProcess(typeof(StrokePaintingRenderer), PostProcessEvent.AfterStack, "Alpacasking/StrokePainting")]
public sealed class StrokePainting : PostProcessEffectSettings
{
    [Tooltip("Canvas texture used to paint."), DisplayName("Canvas")]
    public TextureParameter canvasTexture = new TextureParameter { value = null };

    [Tooltip("Stroke texture used to paint."), DisplayName("Stoke")]
    public TextureParameter strokeTexture = new TextureParameter { value = null };

    [Range(0.001f, 1), Tooltip("Stroke size used to paint."), DisplayName("Stoke Size")]
    public FloatParameter strokeSize = new FloatParameter { value = 0.01f };

    [Range(0.001f, 1), Tooltip("Stroke ratio used to paint."), DisplayName("Stoke Ratio")]
    public FloatParameter strokeRatio = new FloatParameter { value = 1f };

    [UnityEngine.Rendering.PostProcessing.Min(1), Tooltip("Stroke interval used to paint."), DisplayName("Stoke Interval")]
    public IntParameter strokeInterval = new IntParameter { value = 5 };
    public override bool IsEnabledAndSupported(PostProcessRenderContext context)
    {
        return enabled.value
            && canvasTexture.value != null && strokeTexture != null;
    }
}

public struct StrokeData
{
    public Vector4 pos;
    public Color color;
    public Vector4 eigens;
};

[UnityEngine.Scripting.Preserve]
public sealed class StrokePaintingRenderer : PostProcessEffectRenderer<StrokePainting>
{
    private ComputeShader strokeComputeShader;

    private ComputeShader grayScaleShader;

    private ComputeShader tensorShader;

    private ComputeShader gaussianBlurShader;

    private ComputeShader bilateralFilterShader;

    private ComputeShader eigenShader;

    private ComputeBuffer strokeBuffer;

    private ComputeBuffer strokeArgsBuffer;

    private int[] args = new int[] { 0, 1, 0, 0 };

    private int generateStrokeKernelID;

    private int toGrayScaleKernelID;

    private int toTensorKernelID;

    private int gaussianBlurHorizontalKernelID;

    private int gaussianBlurVerticalKernelID;

    private int bilateralFilterHorizontalKernelID;

    private int bilateralFilterVerticalKernelID;


    private int eigenKernelID;

    private int Stride;

    private Material strokeRenderMat;

    private int tempRTID1;

    private int tempRTID2;

    //private int tempCanvas;

    public override void Init()
    {
        strokeComputeShader = (ComputeShader)Resources.Load("ComputeShaders/GenerateStroke");
        grayScaleShader = (ComputeShader)Resources.Load("ComputeShaders/GrayScale");
        tensorShader = (ComputeShader)Resources.Load("ComputeShaders/Tensor");
        gaussianBlurShader = (ComputeShader)Resources.Load("ComputeShaders/GaussianBlur");
        bilateralFilterShader = (ComputeShader)Resources.Load("ComputeShaders/BilateralFilter");
        eigenShader = (ComputeShader)Resources.Load("ComputeShaders/Eigen");
        generateStrokeKernelID = strokeComputeShader.FindKernel("GenerateStroke");
        toGrayScaleKernelID = grayScaleShader.FindKernel("ToGrayScale");
        toTensorKernelID = tensorShader.FindKernel("ToTensor");
        gaussianBlurHorizontalKernelID = gaussianBlurShader.FindKernel("GaussianBlurHorizontal");
        gaussianBlurVerticalKernelID = gaussianBlurShader.FindKernel("GaussianBlurVertical");
        bilateralFilterHorizontalKernelID = bilateralFilterShader.FindKernel("BilateralFilterHorizontal");
        bilateralFilterVerticalKernelID = bilateralFilterShader.FindKernel("BilateralFilterVertical");
        eigenKernelID = eigenShader.FindKernel("Eigen");
        Stride = Marshal.SizeOf(typeof(StrokeData));
        strokeBuffer = new ComputeBuffer(Screen.width * Screen.height, Stride, ComputeBufferType.Append);
        strokeArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
        strokeArgsBuffer.SetData(args);
        strokeRenderMat = new Material(Shader.Find("Hidden/RenderStroke"));
        tempRTID1 = Shader.PropertyToID("tempRTID1");
        tempRTID2 = Shader.PropertyToID("tempRTID2");
        //tempCanvas = Shader.PropertyToID("tempCanvas");
    }
    public override void Release()
    {
        strokeBuffer.Release();
        strokeArgsBuffer.Release();
    }

    public override void Render(PostProcessRenderContext context)
    {
        if (strokeRenderMat == null)
        {
            Init();
        }
        //strokeArgsBuffer.GetData(args);
        //Debug.Log(args[0]);
        strokeBuffer.SetCounterValue(0);
        var cmd = context.command;
        cmd.BeginSample(" StrokePainting");
        RenderTextureDescriptor desc = new RenderTextureDescriptor(context.width, context.height);
        desc.enableRandomWrite = true;
        //cmd.GetTemporaryRT(tempCanvas, desc);
        desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat;
        cmd.GetTemporaryRT(tempRTID1, desc);
        cmd.GetTemporaryRT(tempRTID2, desc);

        //To GrayScale
        cmd.SetComputeTextureParam(grayScaleShader, toGrayScaleKernelID, "Source", context.source);
        cmd.SetComputeTextureParam(grayScaleShader, toGrayScaleKernelID, "Result", tempRTID1);
        cmd.DispatchCompute(grayScaleShader, toGrayScaleKernelID, context.width, context.height, 1);
        //cmd.Blit(tempGrayScaleRTID, context.destination);

        //calculate tensor
        cmd.SetComputeTextureParam(tensorShader, toTensorKernelID, "Source", tempRTID1);
        cmd.SetComputeTextureParam(tensorShader, toTensorKernelID, "Result", tempRTID2);
        cmd.DispatchCompute(tensorShader, toTensorKernelID, context.width, context.height, 1);
        //cmd.Blit(tempRTID2, context.destination);

        //gaussian blur
        cmd.SetComputeIntParam(gaussianBlurShader, "WindowSize", 4);
        cmd.SetComputeFloatParam(gaussianBlurShader, "Sigma", 1f);
        cmd.SetComputeTextureParam(gaussianBlurShader, gaussianBlurHorizontalKernelID, "Source", tempRTID2);
        cmd.SetComputeTextureParam(gaussianBlurShader, gaussianBlurHorizontalKernelID, "Result", tempRTID1);
        cmd.DispatchCompute(gaussianBlurShader, gaussianBlurHorizontalKernelID, context.width, context.height, 1);
        cmd.SetComputeTextureParam(gaussianBlurShader, gaussianBlurVerticalKernelID, "Source", tempRTID1);
        cmd.SetComputeTextureParam(gaussianBlurShader, gaussianBlurVerticalKernelID, "Result", tempRTID2);
        cmd.DispatchCompute(gaussianBlurShader, gaussianBlurVerticalKernelID, context.width, context.height, 1);
        //cmd.Blit(tempRTID2, context.destination);

        //bilateral Filter
        /*cmd.SetComputeIntParam(bilateralFilterShader, "WindowSize", 4);
        cmd.SetComputeFloatParam(bilateralFilterShader, "SigmaSpace", 0.01f);
        cmd.SetComputeFloatParam(bilateralFilterShader, "SigmaColor", 0.01f);
        cmd.SetComputeTextureParam(bilateralFilterShader, bilateralFilterHorizontalKernelID, "Source", tempRTID2);
        cmd.SetComputeTextureParam(bilateralFilterShader, bilateralFilterHorizontalKernelID, "Result", tempRTID1);
        cmd.DispatchCompute(bilateralFilterShader, bilateralFilterHorizontalKernelID, context.width, context.height, 1);
        cmd.SetComputeTextureParam(bilateralFilterShader, bilateralFilterVerticalKernelID, "Source", tempRTID1);
        cmd.SetComputeTextureParam(bilateralFilterShader, bilateralFilterVerticalKernelID, "Result", tempRTID2);
        cmd.DispatchCompute(bilateralFilterShader, bilateralFilterVerticalKernelID, context.width, context.height, 1);*/

        // eigen
        cmd.SetComputeTextureParam(eigenShader, eigenKernelID, "Source", tempRTID2);
        cmd.SetComputeTextureParam(eigenShader, eigenKernelID, "Result", tempRTID1);
        cmd.DispatchCompute(eigenShader, eigenKernelID, context.width, context.height, 1);
        //cmd.Blit(tempRTID1, context.destination);


        // Generate strokes
        //cmd.Blit(settings.canvasTexture.value, tempCanvas);
        //cmd.Blit(context.source, context.destination);
        //cmd.SetRenderTarget(tempCanvas);
        cmd.Blit(settings.canvasTexture.value, context.destination);
        cmd.SetComputeTextureParam(strokeComputeShader, generateStrokeKernelID, "Source", context.source);
        cmd.SetComputeTextureParam(strokeComputeShader, generateStrokeKernelID, "Eigens", tempRTID1);
        cmd.SetComputeIntParam(strokeComputeShader, "StrokeInterval", settings.strokeInterval.value);
        cmd.SetComputeBufferParam(strokeComputeShader, generateStrokeKernelID, "GeneratedStrokes", strokeBuffer);
        cmd.DispatchCompute(strokeComputeShader, generateStrokeKernelID, context.width, context.height, 1);
        cmd.CopyCounterValue(strokeBuffer, strokeArgsBuffer, 0);
        strokeRenderMat.SetBuffer("inputBuffer", strokeBuffer);
        strokeRenderMat.SetTexture("_MainTex", settings.strokeTexture.value);
        strokeRenderMat.SetFloat("_StrokeSize", settings.strokeSize.value);
        strokeRenderMat.SetFloat("_StrokeRatio", settings.strokeRatio.value);
        cmd.DrawProceduralIndirect(Matrix4x4.identity, strokeRenderMat, 0, MeshTopology.Points, strokeArgsBuffer);
        //cmd.CopyTexture(tempCanvas, 0, 0, context.width / 2, 0, context.width / 2, context.height, context.destination, 0, 0, context.width / 2, 0);

        //cmd.SetRenderTarget(context.destination);
        cmd.ReleaseTemporaryRT(tempRTID1);
        cmd.ReleaseTemporaryRT(tempRTID2);
        //cmd.ReleaseTemporaryRT(tempCanvas);
        cmd.EndSample("StrokePainting");
    }
}