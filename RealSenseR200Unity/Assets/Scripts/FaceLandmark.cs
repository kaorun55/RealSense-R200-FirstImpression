using UnityEngine;
using System.Collections;
using RSUnityToolkit;

public class FaceLandmark : MonoBehaviour {

    public GameObject faceObject;
    public Material faceMaterial;

	// Use this for initialization
	void Start () {
        // SenseManagerの生成
        var senseManager = GameObject.FindObjectOfType( typeof( SenseToolkitManager ) );
        if ( senseManager == null ) {
            Debug.LogWarning( "Sense Manager Object not found and was added automatically" );
            senseManager = (GameObject)Instantiate( Resources.Load( "SenseManager" ) );
            senseManager.name = "SenseManager";
        }

        SenseToolkitManager.Instance.SetSenseOption( SenseOption.SenseOptionID.Face );

        // 顔の点用のオブジェクトを作る
        faceObject = new GameObject( "face" );
        faceObject.transform.parent = transform;
    }

    // Update is called once per frame
    void Update () {
        var faces = SenseToolkitManager.Instance.FaceModuleOutput.QueryFaces();
        if( faces.Length == 0 ) {
            return;
        }

        var face = faces[0];
        var landmarks =  face.QueryLandmarks();
        if( landmarks  == null ) {
            return;
        }

        PXCMFaceData.LandmarkPoint[] outPoints;
        landmarks.QueryPoints( out outPoints );
        foreach(var point in outPoints ) {
            if( point.source.alias == PXCMFaceData.LandmarkType.LANDMARK_NOT_NAMED ) {
                continue;
            }

            // 列挙値を名前にする
            var name = point.source.alias.ToString();

            // 対象のGameObjectを探して、なかったら作る
            var f = faceObject.transform.Find( name );
            if ( f == null ) {
                f = GameObject.CreatePrimitive( PrimitiveType.Sphere ).transform;
                f.GetComponent<Renderer>().material = faceMaterial;
                f.gameObject.AddComponent<Rigidbody>();
                f.transform.parent = faceObject.transform;
                f.name = name;
            }

            // 位置と回転を設定
            f.transform.localRotation = Quaternion.Euler( Vector3.zero );
            f.transform.localPosition = new Vector3( -point.world.x * 100, point.world.y * 100, point.world.z * 100 );
        }
    }
}
