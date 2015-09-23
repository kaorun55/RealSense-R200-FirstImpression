using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RealSenseSample
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        PXCMSenseManager senseManager;

        short[] depthBuffer;
        Point point = new Point( 0, 0 );

        int DepthWidth = 0;
        int DepthHeight = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded( object sender, RoutedEventArgs e )
        {
            try {
                Initialize();

                CompositionTarget.Rendering += CompositionTarget_Rendering;
            }
            catch ( Exception ex ) {
                MessageBox.Show( ex.Message );
                Close();
            }
        }

        private void Window_Unloaded( object sender, RoutedEventArgs e )
        {
            Uninitialize();
        }

        private void Initialize()
        {
            // SenseManagerを生成する
            senseManager = PXCMSenseManager.CreateInstance();
            if ( senseManager == null ) {
                throw new Exception( "SenseManagerを生成できませんでした。" );
            }

            // 利用可能なデバイスを列挙する
            PopulateDevice();

            // Depthストリームを有効にする
            pxcmStatus sts = senseManager.EnableStream( PXCMCapture.StreamType.STREAM_TYPE_DEPTH, 0, 0, 0 );
            if ( sts < pxcmStatus.PXCM_STATUS_NO_ERROR ) {
                throw new Exception( "Depthストリームの有効化に失敗しました" );
            }

            // パイプラインを初期化する
            sts =  senseManager.Init();
            if ( sts < pxcmStatus.PXCM_STATUS_NO_ERROR ) {
                throw new Exception( "初期化に失敗しました" );
            }

            // デバイス情報を取得する
            GetDeviceInfo();
        }

        private void Uninitialize()
        {
            if ( senseManager != null ) {
                senseManager.Dispose();
                senseManager = null;
            }
        }

        public void PopulateDevice()
        {
            var desc = new PXCMSession.ImplDesc
            {
                group = PXCMSession.ImplGroup.IMPL_GROUP_SENSOR,
                subgroup = PXCMSession.ImplSubgroup.IMPL_SUBGROUP_VIDEO_CAPTURE
            };

            var session = senseManager.QuerySession();

            for ( int i = 0; ; i++ ) {
                // デバイスのディスクリプタを取得する
                PXCMSession.ImplDesc desc1;
                var ret = session.QueryImpl( desc, i, out desc1 );
                if ( ret < pxcmStatus.PXCM_STATUS_NO_ERROR ) {
                    break;
                }

                // キャプチャーを作成する
                PXCMCapture capture;
                ret = session.CreateImpl( desc1, out capture );
                if ( ret < pxcmStatus.PXCM_STATUS_NO_ERROR ){
                    continue;
                }

                // デバイスを列挙する
                for ( int j = 0; ; j++ ) {
                    PXCMCapture.DeviceInfo dinfo;
                    if ( capture.QueryDeviceInfo( j, out dinfo ) < pxcmStatus.PXCM_STATUS_NO_ERROR ) {
                        break;
                    }

                    // R200 : Intel(R) RealSense(TM) 3D Camera R200
                    // F200 : Intel(R) RealSense(TM) 3D Camera
                    Trace.WriteLine( dinfo.name );
                }

                capture.Dispose();
            }
        }

        private void GetDeviceInfo()
        {
            // デバイスを取得する
            var device = senseManager.QueryCaptureManager().QueryDevice();

            // ミラー表示にする
            // リア側は見たままを表示するのでミラーにしないほうがよい
            //senseManager.QueryCaptureManager().QueryDevice().SetMirrorMode( PXCMCapture.Device.MirrorMode.MIRROR_MODE_HORIZONTAL );

            // 画面の情報を取得する
            PXCMCapture.Device.StreamProfileSet profiles = null;
            device.QueryStreamProfileSet( out profiles );

            DepthWidth = profiles.depth.imageInfo.width;
            DepthHeight = profiles.depth.imageInfo.height;

            // RealSense カメラの情報を取得する
            PXCMCapture.DeviceInfo dinfo;
            device.QueryDeviceInfo( out dinfo );
            if ( dinfo.model == PXCMCapture.DeviceModel.DEVICE_MODEL_F200 ) {
                // DEVICE_MODEL_IVCAMも同じ値
                TextModel.Text = dinfo.model.ToString() + "(F200)";
            }
            else if ( dinfo.model == PXCMCapture.DeviceModel.DEVICE_MODEL_R200 ) {
                // DEVICE_MODEL_DS4も同じ値
                TextModel.Text = dinfo.model.ToString() + "(R200)";
            }

            // カメラの情報を表示する
            TextWidth.Text =  "幅   : " + DepthWidth.ToString();
            TextHeight.Text = "高さ : " + DepthHeight.ToString();

            // 画面の中心座標
            point = new Point( DepthWidth / 2, DepthHeight / 2 );
        }

        void CompositionTarget_Rendering( object sender, EventArgs e )
        {
            try {
                UpdateFrame();
            }
            catch ( Exception ex ) {
                MessageBox.Show( ex.Message );
                Close();
            }
        }

        void UpdateFrame()
        {
            // フレームを取得する
            pxcmStatus ret =  senseManager.AcquireFrame( true );
            if ( ret < pxcmStatus.PXCM_STATUS_NO_ERROR ) {
                return;
            }

            // フレームデータを取得する
            PXCMCapture.Sample sample = senseManager.QuerySample();
            if ( sample != null ) {
                // 各データを表示する
                UpdateDepthImage( sample.depth );
                UpdateDepthData( sample.depth );
                ShowSelectedDepth();
            }

            // フレームを解放する
            senseManager.ReleaseFrame();
        }

        // Depth画像を更新する
        private void UpdateDepthImage( PXCMImage depthFrame )
        {
            if ( depthFrame == null ) {
                return;
            }

            // データを取得する
            PXCMImage.ImageData data;
            pxcmStatus ret =  depthFrame.AcquireAccess( PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32, out data );
            if ( ret < pxcmStatus.PXCM_STATUS_NO_ERROR ) {
                throw new Exception( "Depth画像の取得に失敗" );
            }

            // Bitmapに変換する
            var info = depthFrame.QueryInfo();
            var length = data.pitches[0] * info.height;

            var buffer = data.ToByteArray( 0, length );
            ImageDepth.Source = BitmapSource.Create( info.width, info.height, 96, 96,
                PixelFormats.Bgr32, null, buffer, data.pitches[0] );

            // データを解放する
            depthFrame.ReleaseAccess( data );
        }

        // Depth(距離)データを更新する
        private void UpdateDepthData( PXCMImage depthFrame )
        {
            if ( depthFrame == null ) {
                return;
            }

            // データを取得する
            PXCMImage.ImageData data;
            pxcmStatus ret =  depthFrame.AcquireAccess( PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_DEPTH, out data );
            if ( ret < pxcmStatus.PXCM_STATUS_NO_ERROR ) {
                throw new Exception( "Depth画像の取得に失敗" );
            }

            // Depthデータを取得する
            var info = depthFrame.QueryInfo();
            depthBuffer = data.ToShortArray( 0, info.width * info.height );

            // データを解放する
            depthFrame.ReleaseAccess( data );
        }

        // 選択位置の距離を表示する
        private void ShowSelectedDepth()
        {
            CanvasPoint.Children.Clear();

            // ポイントの位置を表示する
            const int R = 10;
            var ellipse = new Ellipse()
            {
                Width = R,
                Height = R,
                Stroke = Brushes.Red,
                StrokeThickness = 3,
            };
            Canvas.SetLeft( ellipse, point.X - (R/2) );
            Canvas.SetTop( ellipse, point.Y - (R/2) );
            CanvasPoint.Children.Add( ellipse );

            // 距離を表示する
            int index = (int)((point.Y * DepthWidth) + point.X);
            var depth = depthBuffer[index];
            var text = new TextBlock()
            {
                FontSize = 20,
                Foreground = Brushes.Green,
                Text = string.Format( "{0}mm", depth ),
            };
            Canvas.SetLeft( text, point.X );
            Canvas.SetTop( text, point.Y );
            CanvasPoint.Children.Add( text  );
        }

        private void Window_MouseLeftButtonDown(  object sender, System.Windows.Input.MouseButtonEventArgs e )
        {
            point = e.GetPosition( CanvasPoint );
        }
    }
}
