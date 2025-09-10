using UnityEngine;
using System.Collections;

public class RenderTotexture_noise : MonoBehaviour {

	private Texture2D oTexture;
	private RenderTexture rTex ;
	public Material mat;
	private Transform shadows;
	private Material shadow_mat;
	public Transform cl_dome;
	private Material cl_dome_mat;
	private int x;
	private int y;
	private Vector2 prerenderedNoise_res = new Vector2(32,32);
	private bool isHR = false;
	// Use this for initialization
	void Start () {
		if (cl_dome.GetComponent<Renderer>()!=null)
			isHR = false;
		else
			isHR = true;
		//cloudM = this.GetComponent<MeshRenderer>().material;
		rTex = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGB32);
		rTex.wrapMode = TextureWrapMode.Repeat;
		if (!isHR){
			cl_dome_mat = cl_dome.GetComponent<MeshRenderer>().sharedMaterial;//material;//.;
			cl_dome_mat.SetTexture("_NoiseTex",rTex);
		} else {
			for (int i=0;i<cl_dome.GetComponent<DClouds_Control>().number_of_submeshes;i++){
				cl_dome.transform.GetChild(i).GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_NoiseTex",rTex);//material;//.;

			}
		}
		shadows = cl_dome.Find("Shadows");
	
		shadow_mat = shadows.GetComponent<Projector>().material;
		//print(shadow_mat.GetTexture("_NoiseTex"));
		shadow_mat.SetTexture("_NoiseTex",rTex);
		//print(cl_dome.FindChild("Shadows").GetComponent<Projector>().material.GetTexture("_NoiseTex"));
	}
	
	// Update is called once per frame
	void Update () {
		GL.Clear(false, true, Color.clear);
		x+=1;
		if (x>(int)prerenderedNoise_res.x-1){
			x=0;
			y-=1;
			if (y<0)
				y=(int)prerenderedNoise_res.y-1;
		}

		Graphics.Blit(oTexture,rTex,mat);

		//cl_dome_mat.SetTextureOffset("_NoiseTexPR",new Vector2(x,y));
	}

}
