using UnityEngine;
using System.Collections;

//using System;
public class DClouds_Control : MonoBehaviour
{
    [Header("If more than 1, enter")] public int number_of_submeshes = 5;
    private float[] cl_parameters = new float[19];
    private float[] cl_parameters_temp = new float[19];
    public bool drawGUI;
    public float min_ambient;
    private string preset_name = "";
    public Transform sun;
    private string error = "";
    public Texture2D cumulus;
    public Texture2D cumulus2;
    private bool draw_shadows = true;
    private bool draw_shadows_old = true;
    private Transform shadows;
    private Transform noise;
    private Material shadows_material;
    public Texture2D cirrus;
    private float night_fade = 1f;
    private Vector2 scrollPosition = Vector2.zero;
    private int number_of_presets;
    private float time = 0f;

    private bool isHR = false;

    // Use this for initialization
    void Start()
    {
        if (this.GetComponent<Renderer>() != null)
            isHR = false;
        else
            isHR = true;
        shadows = this.transform.Find("Shadows");
        noise = this.transform.Find("Noise_plane");
        shadows_material = new Material(shadows.GetComponent<Projector>().material);
        shadows.GetComponent<Projector>().material =
            shadows_material; //just making sure that material instance is used and runtime changes do not affect basic material(for some reason instances weren't created automatically in unity 5.3) 
        number_of_presets = PlayerPrefs.GetInt("presets_n");
        GetParameters();
        SetProjectorParametes();
        //PlayerPrefs.DeleteAll();
        RandomUV();
        LoadPreset("Cumulus Sunset");
    }

    void RandomUV()
    {
        Vector2 rand = Random.insideUnitCircle * 100f + new Vector2(100f, 100f);
        if (!isHR)
            this.GetComponent<Renderer>().material.SetVector("_Rnd", new Vector4(rand.x, rand.y, 0f, 0f));
        else
            for (int i = 0; i < number_of_submeshes; i++)
                this.transform.GetChild(i).GetComponent<Renderer>().material.SetVector("_Rnd", new Vector4(rand.x, rand.y, 0f, 0f));
        shadows.GetComponent<Projector>().material.SetVector("_Rnd", new Vector4(rand.x, rand.y, 0f, 0f));
    }

    void GetParameters()
    {
        if (!isHR)
        {
            cl_parameters[0] = this.GetComponent<Renderer>().material.GetFloat("_Exposure");
            cl_parameters[1] = this.GetComponent<Renderer>().material.GetFloat("_Density");
            cl_parameters[2] = this.GetComponent<Renderer>().material.GetFloat("_Height");
            cl_parameters[3] = this.GetComponent<Renderer>().material.GetFloat("_Cutout");
            cl_parameters[4] = this.GetComponent<Renderer>().material.GetFloat("_Transparency");
            cl_parameters[5] = this.GetComponent<Renderer>().material.GetFloat("_Translucency");
            cl_parameters[6] = this.GetComponent<Renderer>().material.GetFloat("_LightK");
            cl_parameters[7] = this.GetComponent<Renderer>().material.GetFloat("_Tiling");
            cl_parameters[8] = this.GetComponent<Renderer>().material.GetFloat("_WindSpeed_X");
            cl_parameters[9] = this.GetComponent<Renderer>().material.GetFloat("_WindSpeed_Y");
            cl_parameters[10] = noise.GetComponent<Renderer>().sharedMaterial.GetFloat("_CloudAnimation");
            cl_parameters[11] = sun.transform.eulerAngles.x / 15; //in hours
            cl_parameters[12] = sun.transform.eulerAngles.y; //azimuth
            if (this.GetComponent<Renderer>().material.GetTexture("_MainTex").name == "cumulus")
                cl_parameters[13] = 0;
            else if (this.GetComponent<Renderer>().material.GetTexture("_MainTex").name == "cirrus")
                cl_parameters[13] = 2;
            else if (this.GetComponent<Renderer>().material.GetTexture("_MainTex").name == "cumulus2")
                cl_parameters[13] = 1;
            cl_parameters[14] = this.GetComponent<Renderer>().material.GetFloat("_TextureBlend");
            cl_parameters[15] = this.GetComponent<Renderer>().material.GetFloat("_TextureTiling");
            cl_parameters[16] = this.GetComponent<Renderer>().material.GetFloat("_AddNoise");
            cl_parameters[17] = this.GetComponent<Renderer>().material.GetFloat("_Contrast");


            for (int i = 0; i < cl_parameters_temp.Length; i++)
                cl_parameters_temp[i] = cl_parameters[i];
        }
        else
        {
            cl_parameters[0] = this.transform.GetChild(0).GetComponent<Renderer>().material.GetFloat("_Exposure");
            cl_parameters[1] = this.transform.GetChild(0).GetComponent<Renderer>().material.GetFloat("_Density");
            cl_parameters[2] = this.transform.GetChild(0).GetComponent<Renderer>().material.GetFloat("_Height");
            cl_parameters[3] = this.transform.GetChild(0).GetComponent<Renderer>().material.GetFloat("_Cutout");
            cl_parameters[4] = this.transform.GetChild(0).GetComponent<Renderer>().material.GetFloat("_Transparency");
            cl_parameters[5] = this.transform.GetChild(0).GetComponent<Renderer>().material.GetFloat("_Translucency");
            cl_parameters[6] = this.transform.GetChild(0).GetComponent<Renderer>().material.GetFloat("_LightK");
            cl_parameters[7] = this.transform.GetChild(0).GetComponent<Renderer>().material.GetFloat("_Tiling");
            cl_parameters[8] = this.transform.GetChild(0).GetComponent<Renderer>().material.GetFloat("_WindSpeed_X");
            cl_parameters[9] = this.transform.GetChild(0).GetComponent<Renderer>().material.GetFloat("_WindSpeed_Y");
            cl_parameters[10] = noise.GetComponent<Renderer>().sharedMaterial.GetFloat("_CloudAnimation");
            cl_parameters[11] = sun.transform.eulerAngles.x / 15; //in hours
            cl_parameters[12] = sun.transform.eulerAngles.y; //azimuth
            if (this.transform.GetChild(0).GetComponent<Renderer>().material.GetTexture("_MainTex").name == "cumulus")
                cl_parameters[13] = 0;
            else if (this.transform.GetChild(0).GetComponent<Renderer>().material.GetTexture("_MainTex").name == "cirrus")
                cl_parameters[13] = 2;
            else if (this.transform.GetChild(0).GetComponent<Renderer>().material.GetTexture("_MainTex").name == "cumulus2")
                cl_parameters[13] = 1;
            cl_parameters[14] = this.transform.GetChild(0).GetComponent<Renderer>().material.GetFloat("_TextureBlend");
            cl_parameters[15] = this.transform.GetChild(0).GetComponent<Renderer>().material.GetFloat("_TextureTiling");
            cl_parameters[16] = this.transform.GetChild(0).GetComponent<Renderer>().material.GetFloat("_AddNoise");
            cl_parameters[17] = this.transform.GetChild(0).GetComponent<Renderer>().material.GetFloat("_Contrast");


            for (int i = 0; i < cl_parameters_temp.Length; i++)
                cl_parameters_temp[i] = cl_parameters[i];
        }
    }

    void SetProjectorParametes()
    {
        if (draw_shadows)
        {
            shadows.GetComponent<Projector>().material.SetFloat("_Density", cl_parameters[1]);

            shadows.GetComponent<Projector>().material.SetFloat("_Cutout", Mathf.Max(0f, cl_parameters[3] - .05f));
            shadows.GetComponent<Projector>().material.SetFloat("_Transparency", cl_parameters[4]);
            shadows.GetComponent<Projector>().material.SetFloat("_Tiling", cl_parameters[7]);
            shadows.GetComponent<Projector>().material.SetFloat("_WindSpeed_X", cl_parameters[8]);
            shadows.GetComponent<Projector>().material.SetFloat("_WindSpeed_Y", cl_parameters[9]);
            //shadows.GetComponent<Projector>().material.SetFloat("_CloudAnimation",cl_parameters[10]);
        }
    }

    void SetCloudsParametes()
    {
        if (!isHR)
        {
            this.GetComponent<Renderer>().material.SetFloat("_Exposure", cl_parameters[0]);
            this.GetComponent<Renderer>().material.SetFloat("_Density", cl_parameters[1]);
            this.GetComponent<Renderer>().material.SetFloat("_Height", cl_parameters[2]);
            this.GetComponent<Renderer>().material.SetFloat("_Cutout", cl_parameters[3]);
            this.GetComponent<Renderer>().material.SetFloat("_Transparency", cl_parameters[4]);
            this.GetComponent<Renderer>().material.SetFloat("_Translucency", cl_parameters[5]);
            this.GetComponent<Renderer>().material.SetFloat("_LightK", cl_parameters[6]);
            this.GetComponent<Renderer>().material.SetFloat("_Tiling", cl_parameters[7]);
            this.GetComponent<Renderer>().material.SetFloat("_WindSpeed_X", cl_parameters[8]);
            this.GetComponent<Renderer>().material.SetFloat("_WindSpeed_Y", cl_parameters[9]);
            noise.GetComponent<Renderer>().sharedMaterial.SetFloat("_CloudAnimation", cl_parameters[10]);
            this.GetComponent<Renderer>().material.SetFloat("_TextureBlend", cl_parameters[14]);
            //print(cl_parameters[13]);
            if (cl_parameters[13] == 0f)
                this.GetComponent<Renderer>().material.SetTexture("_MainTex", cumulus);
            else if (cl_parameters[13] == 2f)
                this.GetComponent<Renderer>().material.SetTexture("_MainTex", cirrus);
            else if (cl_parameters[13] == 1f)
                this.GetComponent<Renderer>().material.SetTexture("_MainTex", cumulus2);

            this.GetComponent<Renderer>().material.SetFloat("_TextureTiling", cl_parameters[15]);
            this.GetComponent<Renderer>().material.SetFloat("_AddNoise", cl_parameters[16]);
            this.GetComponent<Renderer>().material.SetFloat("_Contrast", cl_parameters[17]);
        }
        else
        {
            for (int i = 0; i < number_of_submeshes; i++)
            {
                this.transform.GetChild(i).GetComponent<Renderer>().material.SetFloat("_Exposure", cl_parameters[0]);
                this.transform.GetChild(i).GetComponent<Renderer>().material.SetFloat("_Density", cl_parameters[1]);
                this.transform.GetChild(i).GetComponent<Renderer>().material.SetFloat("_Height", cl_parameters[2]);
                this.transform.GetChild(i).GetComponent<Renderer>().material.SetFloat("_Cutout", cl_parameters[3]);
                this.transform.GetChild(i).GetComponent<Renderer>().material.SetFloat("_Transparency", cl_parameters[4]);
                this.transform.GetChild(i).GetComponent<Renderer>().material.SetFloat("_Translucency", cl_parameters[5]);
                this.transform.GetChild(i).GetComponent<Renderer>().material.SetFloat("_LightK", cl_parameters[6]);
                this.transform.GetChild(i).GetComponent<Renderer>().material.SetFloat("_Tiling", cl_parameters[7]);
                this.transform.GetChild(i).GetComponent<Renderer>().material.SetFloat("_WindSpeed_X", cl_parameters[8]);
                this.transform.GetChild(i).GetComponent<Renderer>().material.SetFloat("_WindSpeed_Y", cl_parameters[9]);
                noise.GetComponent<Renderer>().sharedMaterial.SetFloat("_CloudAnimation", cl_parameters[10]);
                this.transform.GetChild(i).GetComponent<Renderer>().material.SetFloat("_TextureBlend", cl_parameters[14]);
                //print(cl_parameters[13]);
                if (cl_parameters[13] == 0f)
                    this.transform.GetChild(i).GetComponent<Renderer>().material.SetTexture("_MainTex", cumulus);
                else if (cl_parameters[13] == 2f)
                    this.transform.GetChild(i).GetComponent<Renderer>().material.SetTexture("_MainTex", cirrus);
                else if (cl_parameters[13] == 1f)
                    this.transform.GetChild(i).GetComponent<Renderer>().material.SetTexture("_MainTex", cumulus2);

                this.transform.GetChild(i).GetComponent<Renderer>().material.SetFloat("_TextureTiling", cl_parameters[15]);
                this.transform.GetChild(i).GetComponent<Renderer>().material.SetFloat("_AddNoise", cl_parameters[16]);
                this.transform.GetChild(i).GetComponent<Renderer>().material.SetFloat("_Contrast", cl_parameters[17]);
            }
        }
    }

    void SetSunParameters()
    {
        sun.transform.eulerAngles = new Vector3(cl_parameters[11] * 15, cl_parameters[12], sun.transform.eulerAngles.z);
        /*float density = Mathf.Pow(cl_parameters[1]/2f,4f);
        print(density);
            if (density<.45f)
                density-= Mathf.Pow(Mathf.Pow(cl_parameters[3]/6f,.5f),1f+4f*cl_parameters[1])/2f;
        density = Mathf.Max(0f,density);
*/
        float density = Mathf.Pow(Mathf.Min(Mathf.Max(0f, cl_parameters[1] - 1f), 1f), 2f * (2f - 1f / (cl_parameters[3] * 2f + 1f)));
        //print(density);
        float angle = -sun.transform.eulerAngles.x + 180f;
        //angle = angle<0f ? 360f-angle : angle = angle;

        //angle = angle>-25f || angle <-155f ? 
        night_fade = angle < 0 ? (Mathf.Pow((1f - Mathf.Abs(angle / 180)) * 2f, .5f)) : 0f;
        float night_fade_pos = angle > 0 ? ((1f - Mathf.Abs(angle / 180)) * 2f) : 0f;

        //night_fade = (? Mathf.Pow((1f-  Mathf.Abs(angle/180f))*2f  ,.25f) : 0f)*.75f +.25f;

        //	print(angle +" "+night_fade_pos);
        sun.GetComponent<Light>().intensity = (Mathf.Pow(1 - density, 1f) > .5f ? Mathf.Pow(1 - density, 1f) : Mathf.Pow(1 - density, 1f));
        //old//RenderSettings.ambientIntensity = Mathf.Max(min_ambient,Mathf.Pow(density,.25f)-night_fade*(1+density));
        RenderSettings.ambientIntensity = 1f;
        float c = Mathf.Max(min_ambient, Mathf.Pow(density, .25f) - night_fade * (1 + density));
        RenderSettings.ambientSkyColor = new Color(c, c, c, c);
    }

    // Update is called once per frame
    void Update()
    {
        // Timelapse code
        /*
        cl_parameters_temp[11]+=.0025f;
        cl_parameters_temp[8] = 3f;
        cl_parameters_temp[10] = 2f;
        cl_parameters_temp[4] =1f;
        cl_parameters_temp[1] += Mathf.Cos((Time.time+12)/5.5f)/6000f;
        cl_parameters_temp[3] += Mathf.Cos(Time.time/1.5f)/1000f;
        //print(Time.time);
        check_parameters();
*/
        //

        //if (drawGUI){
        //print(cl_parameters[cl_parameters.Length-1] +" "+cl_parameters_temp[cl_parameters.Length-1]);
        if (cl_parameters[cl_parameters.Length - 1] != cl_parameters_temp[cl_parameters.Length - 1])
        {
            //print("Setting");
            for (int i = 0; i < cl_parameters_temp.Length; i++)
                cl_parameters[i] = cl_parameters_temp[i];
            SetCloudsParametes();
            SetProjectorParametes();
            SetSunParameters();
        }

        if (draw_shadows != draw_shadows_old)
        {
            draw_shadows_old = draw_shadows;
            if (!draw_shadows)
                shadows.gameObject.SetActive(false);
            if (draw_shadows)
            {
                shadows.gameObject.SetActive(true);
                SetProjectorParametes();
            }
        }
        //}
    }

    void SavePreset(string name)
    {
        if (!PlayerPrefs.HasKey(name.ToString() + "_cloud_p_" + "0"))
        {
            PlayerPrefs.SetString("id" + PlayerPrefs.GetInt("presets_n").ToString(), name);
            number_of_presets++;
            PlayerPrefs.SetInt("presets_n", number_of_presets);
        }

        for (int i = 0; i < cl_parameters.Length; i++)
            PlayerPrefs.SetFloat(name.ToString() + "_cloud_p_" + i.ToString(), cl_parameters[i]);
        error = "";
    }

    void DeletePreset(int n)
    {
        string name_preset = PlayerPrefs.GetString("id" + n.ToString());
        for (int i = 0; i < cl_parameters.Length; i++)
        {
            if (PlayerPrefs.HasKey(name_preset.ToString() + "_cloud_p_" + i.ToString()))
            {
                PlayerPrefs.DeleteKey(name_preset.ToString() + "_cloud_p_" + i.ToString());
            }
            else
            {
                error = "wrong name";
            }
        }

        MoveListUp(n);
        number_of_presets--;
        PlayerPrefs.SetInt("presets_n", number_of_presets);
    }

    void MoveListUp(int n)
    {
        // moving the list on cell up after deletion
        if (n < number_of_presets - 1)
        {
            for (int i = n + 1; i < number_of_presets; i++)
            {
                PlayerPrefs.SetString("id" + (i - 1).ToString(), PlayerPrefs.GetString("id" + i.ToString()));
            }
        }

        PlayerPrefs.DeleteKey("id" + (number_of_presets - 1).ToString());
    }

    void LoadPreset(string name)
    {
        //PlayerPrefs.SetFloat(name.ToString(), cl_parameters[i]);

        for (int i = 0; i < cl_parameters.Length; i++)
        {
            if (PlayerPrefs.HasKey(name.ToString() + "_cloud_p_" + i.ToString()))
                cl_parameters_temp[i] = PlayerPrefs.GetFloat(name.ToString() + "_cloud_p_" + i.ToString());
            else
            {
                error = "wrong name";
                break;
            }

            preset_name = name;
            error = "";
        }
    }

    void OnGUI()
    {
        time = cl_parameters[11] + 8 > 24 ? cl_parameters[11] + 8 - 24 : cl_parameters[11] + 8;

        if (drawGUI)
        {
            GUI.Label(new Rect(50, 10, 200, 20), "Time of the day: " + Mathf.Floor(time) + " " + Mathf.Floor(60f * (time - Mathf.Floor(time))));
            cl_parameters_temp[11] = GUI.HorizontalSlider(new Rect(50, 30, 130, 20), cl_parameters_temp[11], 0f, 23.99f);
            GUI.Label(new Rect(200, 10, 200, 20), "Azimuth:" + cl_parameters_temp[12]);
            cl_parameters_temp[12] = GUI.HorizontalSlider(new Rect(200, 30, 100, 20), cl_parameters_temp[12], -180f, 180f);
            GUI.Label(new Rect(Screen.width - 250, 20, 250, 50), "Hold and move mouse to rotate camera");

            GUI.Label(new Rect(Screen.width - 200, 50, 200, 50), "Enter a preset name:");

            preset_name = GUI.TextField(new Rect(Screen.width - 200, 80, 190, 20), preset_name, 15);
            if (GUI.Button(new Rect(Screen.width - 200, 120, 90, 50), "Save Preset"))
            {
                if (preset_name != "")
                    SavePreset(preset_name);
                else
                    error = "Enter name";
            }

            if (GUI.Button(new Rect(Screen.width - 100, 120, 90, 50), "Load Preset"))
            {
                if (preset_name != "")
                    LoadPreset(preset_name);
                else
                    error = "Enter name";
            }

            GUI.Label(new Rect(Screen.width - 200, 180, 200, 50), error);

            scrollPosition = GUI.BeginScrollView(new Rect(Screen.width - 350, 220, 345, 210), scrollPosition, new Rect(0, 0, 210, 35 * number_of_presets));
            //print(number_of_presets);

            for (int i = 0; i < number_of_presets; i++)
            {
                if (GUI.Button(new Rect(0, i * 35, 160, 30), PlayerPrefs.GetString("id" + i.ToString())))
                {
                    LoadPreset(PlayerPrefs.GetString("id" + i.ToString()));
                }

                if (GUI.Button(new Rect(170, i * 35, 160, 30), "Delete " + PlayerPrefs.GetString("id" + i.ToString())))
                    DeletePreset(i);
            }

            GUI.EndScrollView();
            if (GUI.Button(new Rect(Screen.width - 180, 500, 160, 30), "Regenerate noise"))
                RandomUV();
            GUI.Label(new Rect(50, 40, 250, 20), "Exposure:" + cl_parameters_temp[0]);
            cl_parameters_temp[0] = GUI.HorizontalSlider(new Rect(50, 55, 130, 20), cl_parameters_temp[0], 0f, 3f);
            GUI.Label(new Rect(50, 70, 250, 20), "Density:" + cl_parameters_temp[1]);
            cl_parameters_temp[1] = GUI.HorizontalSlider(new Rect(50, 85, 130, 20), cl_parameters_temp[1], 0f, 2f);
            GUI.Label(new Rect(50, 100, 250, 20), "Height:" + cl_parameters_temp[2]);
            cl_parameters_temp[2] = GUI.HorizontalSlider(new Rect(50, 115, 130, 20), cl_parameters_temp[2], 0.1f, 1f);
            GUI.Label(new Rect(50, 130, 250, 20), "Cutout:" + cl_parameters_temp[3]);
            cl_parameters_temp[3] = GUI.HorizontalSlider(new Rect(50, 145, 130, 20), cl_parameters_temp[3], 0.1f, 8f);
            GUI.Label(new Rect(50, 160, 250, 20), "Transparency:" + cl_parameters_temp[4]);
            cl_parameters_temp[4] = GUI.HorizontalSlider(new Rect(50, 175, 130, 20), cl_parameters_temp[4], 0f, 1f);
            GUI.Label(new Rect(50, 190, 250, 20), "Translucency" + cl_parameters_temp[5]);
            cl_parameters_temp[5] = GUI.HorizontalSlider(new Rect(50, 205, 130, 20), cl_parameters_temp[5], 0.1f, 1f);
            GUI.Label(new Rect(50, 220, 250, 20), "Light influence:" + cl_parameters_temp[6]);
            cl_parameters_temp[6] = GUI.HorizontalSlider(new Rect(50, 235, 130, 20), cl_parameters_temp[6], 0f, 1f);
            GUI.Label(new Rect(50, 250, 250, 20), "Cloud tiling:" + cl_parameters_temp[7]);
            cl_parameters_temp[7] = Mathf.Round(GUI.HorizontalSlider(new Rect(50, 265, 130, 20), cl_parameters_temp[7], 1f, 32f));
            GUI.Label(new Rect(50, 280, 250, 20), "Wind speed X:" + cl_parameters_temp[8]);
            cl_parameters_temp[8] = GUI.HorizontalSlider(new Rect(50, 295, 130, 20), cl_parameters_temp[8], -2f, 2f);
            GUI.Label(new Rect(50, 310, 250, 20), "Wind Speed Y:" + cl_parameters_temp[9]);
            cl_parameters_temp[9] = GUI.HorizontalSlider(new Rect(50, 325, 130, 20), cl_parameters_temp[9], -2f, 2f);
            GUI.Label(new Rect(50, 340, 250, 20), "Animation Speed:" + cl_parameters_temp[10]);
            cl_parameters_temp[10] = GUI.HorizontalSlider(new Rect(50, 365, 130, 20), cl_parameters_temp[10], 0f, 2f);
            string cl_tex = cl_parameters_temp[13] == 0 ? "Cumulus" : cl_parameters_temp[13] == 1 ? "Cumulus2" : cl_parameters_temp[13] == 2 ? "Cirrus" : "Wrong Texture";
            GUI.Label(new Rect(50, 380, 250, 20), "Cloud texture: " + cl_tex);
            cl_parameters_temp[13] = Mathf.Round(GUI.HorizontalSlider(new Rect(50, 395, 130, 20), cl_parameters_temp[13], 0f, 2f));
            GUI.Label(new Rect(50, 410, 250, 20), "Texture blending: " + cl_parameters_temp[14]);
            cl_parameters_temp[14] = GUI.HorizontalSlider(new Rect(50, 425, 130, 20), cl_parameters_temp[14], 0f, 1f);
            GUI.Label(new Rect(50, 440, 250, 20), "Contrast: " + cl_parameters_temp[17]);
            cl_parameters_temp[17] = GUI.HorizontalSlider(new Rect(50, 455, 130, 20), cl_parameters_temp[17], 0f, 10f);

            GUI.Label(new Rect(200, 40, 250, 20), "Texture additional tilling: " + cl_parameters_temp[15]);
            cl_parameters_temp[15] = Mathf.Round(GUI.HorizontalSlider(new Rect(200, 55, 130, 20), cl_parameters_temp[15], 0.1f, 8f));
            //GUI.Label(new Rect(200,70,250,20),"Additional Noise: " + cl_parameters_temp[16]);

            cl_parameters_temp[16] =
                GUI.Toggle(new Rect(200, 70, 250, 20), cl_parameters_temp[16] == 1 ? true : false, "Additional Noise: ")
                    ? 1
                    : 0; //Mathf.Round(GUI.HorizontalSlider(new Rect(200,85,130,20),cl_parameters_temp[16] ,0f,1f));
            draw_shadows = GUI.Toggle(new Rect(200, 100, 250, 20), draw_shadows, "Draw shadows");
            check_parameters();
        }
    }

    void check_parameters()
    {
        cl_parameters_temp[cl_parameters.Length - 1] = 0;
        for (int i = 0; i < cl_parameters_temp.Length; i++)
            cl_parameters_temp[cl_parameters.Length - 1] += cl_parameters_temp[i];
    }
}