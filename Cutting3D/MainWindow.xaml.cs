using Accessibility;
using Kitware.VTK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;



namespace Cutting3D
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Read_Struct RTStructFile ;
        ObservableCollection<DicomSlice> dicomSlices; //Auto Filled
        double diffBoundY;
        
        public MainWindow()
        {
            InitializeComponent();
            RTStructFile = new Read_Struct();
            dicomSlices = new ObservableCollection<DicomSlice>() { };
            
        }

        private void WindowsFormsHost_Loaded(object sender, RoutedEventArgs e)
        {
            /*renderWindowControl.RenderWindow.Render();
            renderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer().Render();*/
            //string fileName = @"D:\DicomImages\A990410-BREAST\Study_1";
            string fileName = @"D:\DicomImages\A990428-BREAST2_MOAFI^KOLSUM\Study_1";
            //string fileName = @"D:\DicomImages\A990508-MEDIASTIN\Study_1";
            //string fileName = @"D:\DicomImages\AE990505-1TABAR\Study_1";
            //string fileName = @"D:\DicomImages\AE990507-1VAZIRI\Study_1";
            ReadStudy(fileName);
        }

        private void ReadStudy(string filename)
        {
            vtkDICOMImageReader reader = new vtkDICOMImageReader();
            reader.SetDirectoryName(filename);
            reader.Update();
            var patientPos = reader.GetImagePositionPatient();
            var renderWindow = renderWindowControl.RenderWindow;
            //string RTStructPath = @"D:\DicomImages\A990410-BREAST\Study_1\RAZEH GOUKEH^OZRA__RTS.dcm";
            string RTStructPath = @"D:\DicomImages\A990428-BREAST2_MOAFI^KOLSUM\Study_1\MOAFI^KOLSUM__RTS.dcm";
            //string RTStructPath = @"D:\DicomImages\A990508-MEDIASTIN\Study_1\ZEYNALI^TAHERE__RTS.dcm";
            //string RTStructPath = @"D:\DicomImages\AE990505-1TABAR\Study_1\SOLEYMANI TABAR ^PARASTOO__RTS.dcm";
            //string RTStructPath = @"D:\DicomImages\AE990507-1VAZIRI\Study_1\VAZIRI^AAZAM__RTS.dcm";
            
            vtkImageData originalImageData = reader.GetOutput();
            originalImageData.SetOrigin(patientPos[0], patientPos[1], patientPos[2]);
            var readRT = RTStructFile.GetStructList(RTStructPath);

            RemoveTableService removeTableService = new RemoveTableService();
            vtkImageData removetable = 
                removeTableService.Execute(originalImageData,readRT.Find(x => x.Key.Equals("Skin")).Value);
            
            /*if (originalImageData.GetBounds()[2] < 0)
            {
                diffBoundY = originalImageData.GetBounds()[3] + originalImageData.GetBounds()[2];
            }
            else
            {
                diffBoundY = originalImageData.GetBounds()[3] - originalImageData.GetBounds()[2];
            }
            getDicomSlices(originalImageData, RTStructPath);
            //------Cropp one by one -----------
            double[] origin = originalImageData.GetOrigin();
            double[] dicomSpacing = originalImageData.GetSpacing();
            int[] dicomExtend = originalImageData.GetExtent();
*/
            
            
            /*croppedOneByOne(origin,dicomSpacing,dicomExtend);
            appendImages();*/
            displayCroppedDicoms(removetable);
            //test();
        }
        
        private void getDicomSlices(vtkImageData originalImageData , string rtStructPath)
        {
            int[] dimentions = originalImageData.GetDimensions();
            
            //Get Stencils and filled dicomSlice collection
            getDicomSkins(rtStructPath, dimentions[2]);

            //Convert dicom directory to slice
            
            for(int z = 0; z< dimentions[2];z++) 
            {
                vtkExtractVOI extractFilter = vtkExtractVOI.New();
                extractFilter.SetInputData(originalImageData);
                extractFilter.SetVOI(0, dimentions[0] - 1, 0, dimentions[1] - 1, z, z);
                extractFilter.Update();

                vtkImageData sliceImage = extractFilter.GetOutput();
                //var selectSlice = dicomSlices.First(slice => slice.sliceId.Equals(z+1));
                var selectSlice = dicomSlices[dimentions[2]-z-1];
                selectSlice.imageData = sliceImage;
            }
            

        }

        private void getDicomSkins(string rtStructPath, int totalSlices)
        {
            List<List<OpenCvSharp.Point2f>> stencils;
            var readRT = RTStructFile.GetStructList(rtStructPath);
            var skins = readRT.Find(x => x.Key.Equals("Skin")).Value;

            //count all contours
            int countContour = skins.SliceContours.Count;
            
            //Grouped skins by Z-position
            var groupedSkins = skins.SliceContours.GroupBy(contour => contour.Z);
            if (totalSlices.Equals(groupedSkins.Count()))
            {
                int i = totalSlices;
                foreach (var slice in groupedSkins)
                {
                    stencils = new List<List<OpenCvSharp.Point2f>>();
                    foreach (var skin in slice)
                    {
                        stencils.Add(skin.ContourPoints);
                    }
                    DicomSlice dicomSlice = new DicomSlice()
                    {
                        sliceId = i,
                        skins = stencils,
                        position = slice.Key
                    };
                    i--;
                    dicomSlices.Add(dicomSlice);
                }
            }
            
        }

        private vtkPolyData makePolyLine(int slice)
        {
            var dicom = dicomSlices.First(s => s.sliceId.Equals(slice));
            var skinData = dicom.skins;
            int startPoint = 0;
            int totalPoint = 0;
            vtkPoints curvePoints = new vtkPoints();
            vtkCellArray curveCells = new vtkCellArray();
            foreach(var skin in skinData)
            {
                foreach (var point in skin)
                {
                    var x = point.X;
                    var y = point.Y;
                    var z = dicom.imageData.GetBounds()[5];
                    curvePoints.InsertNextPoint(x, -y, z);
                }

                totalPoint += skin.Count();
                curveCells.InsertNextCell(skin.Count());
                for (var point = startPoint; point < totalPoint; point++)
                {
                    curveCells.InsertCellPoint(point);
                }
                startPoint += skin.Count();
            }
            vtkPolyData polyData = new vtkPolyData();
            polyData.SetPoints(curvePoints);
            polyData.SetLines(curveCells);

            //Transform poly data
            float miny = skinData.Min(p => p.Min(m => m.Y));
            float maxy = skinData.Max(p => p.Max(m => m.Y));
            float diffy = (-miny)-maxy ;
            float minx = skinData.Min(p => p.Min(m => m.X));
            vtkTransform transform = vtkTransform.New();
            //transform.Translate(214, 256, 0);// image lenght 428x512
            transform.Translate(0, diffBoundY, 0);
            //transform.Translate(0, 0, 0);

            vtkTransformPolyDataFilter polylineTransform = new vtkTransformPolyDataFilter();
            polylineTransform.SetInputData(polyData);
            polylineTransform.SetTransform(transform);
            polylineTransform.Update();

            vtkPolyData curve = polylineTransform.GetOutput();
            return curve;
        }
        private void croppedOneByOne(double[] origin, double[] dicomSpacing, int[] dicomExtend)
        {
            fillPolylineProp();
            foreach(var ds in dicomSlices)
            {
                // Voxelization: Convert polyline to image stencil
                vtkPolyDataToImageStencil polyToStencil = vtkPolyDataToImageStencil.New();
                polyToStencil.SetInputData(ds.polyLine);
                polyToStencil.SetOutputOrigin(origin[0], origin[1], origin[2]);
                polyToStencil.SetOutputSpacing(dicomSpacing[0], dicomSpacing[1], dicomSpacing[2]);
                polyToStencil.SetOutputWholeExtent(dicomExtend[0], dicomExtend[1], dicomExtend[2], dicomExtend[3], dicomExtend[4], dicomExtend[5]);
                polyToStencil.Update();

                vtkImageStencilData stencilData = polyToStencil.GetOutput();

                // Apply stencil to the DICOM image
                vtkImageStencil imageStencil = vtkImageStencil.New();
                imageStencil.SetInputData(ds.imageData);
                imageStencil.SetStencilData(stencilData);
                //imageStencil.ReverseStencilOn();
                imageStencil.Update();


                vtkImageData result = imageStencil.GetOutput();
                ds.croppedImageData = result;
            }
            
           
        }
        private void fillPolylineProp()
        {
            int sliceNumber = dicomSlices.Count();
            
            for (int i = 1; i<=sliceNumber;i++)
            {
                
                var polylineTest = makePolyLine(i);
                dicomSlices.First(slice => slice.sliceId.Equals(i)).polyLine = polylineTest;

                //dicomSlices[i].polyLine = polylineTest;
            }
            
        }
        private vtkImageData cuttingAllDicomes(vtkImageData imageData, vtkPolyData polyLine)
        {


            double[] i = imageData.GetOrigin();
            double[] dicomSpacing = imageData.GetSpacing();
            int[] dicomExtend = imageData.GetExtent();

            // Voxelization: Convert polyline to image stencil
            vtkPolyDataToImageStencil polyToStencil = vtkPolyDataToImageStencil.New();
            polyToStencil.SetInputData(polyLine);
            polyToStencil.SetOutputOrigin(i[0], i[1], i[2]);
            polyToStencil.SetOutputSpacing(dicomSpacing[0], dicomSpacing[1], dicomSpacing[2]);
            polyToStencil.SetOutputWholeExtent(dicomExtend[0], dicomExtend[1], dicomExtend[2], dicomExtend[3], dicomExtend[4], dicomExtend[5]);
            polyToStencil.Update();

            vtkImageStencilData stencilData = polyToStencil.GetOutput();

            // Apply stencil to the DICOM image
            vtkImageStencil imageStencil = vtkImageStencil.New();
            imageStencil.SetInputData(imageData);
            imageStencil.SetStencilData(stencilData);
            //imageStencil.ReverseStencilOn();
            imageStencil.Update();


            vtkImageData result = imageStencil.GetOutput();
            return result;
        }
        private vtkPolyData allCurves()
        {
            Read_Struct dicomRT = new Read_Struct();
            var RTstructs = dicomRT.GetStructList(@"D:\DicomImages\A990410-BREAST\Study_1\RAZEH GOUKEH^OZRA__RTS.dcm");
            var skinsData = RTstructs.First(x => x.Key.Equals("Skin")).Value;
            vtkPoints curvePoints = new vtkPoints();
            vtkCellArray curveCells = new vtkCellArray();
            skinsData.SliceContours.Reverse();
            var groupedSkins = skinsData.SliceContours.GroupBy(contour => contour.Z).ToArray();
            int zcoord = 0;
            foreach (var sData in groupedSkins)
            {
                int startPoint = 0;
                int totalPoint = 0;
                foreach (var skin in sData)
                {
                    foreach (var point in skin.ContourPoints)
                    {
                        var x = point.X;
                        var y = point.Y;
                        var z = sData.Key;
                        //var z = zcoord;
                        curvePoints.InsertNextPoint(x, -y, z);
                    }

                    totalPoint += skin.ContourPoints.Count();
                    curveCells.InsertNextCell(skin.ContourPoints.Count());
                    for (var point = startPoint; point < totalPoint; point++)
                    {
                        curveCells.InsertCellPoint(point);
                    }
                    startPoint += skin.ContourPoints.Count();
                }
                //zcoord += 5;
            }


            vtkPolyData polyData = new vtkPolyData();
            polyData.SetPoints(curvePoints);
            polyData.SetLines(curveCells);

            vtkTransform transform = vtkTransform.New();
            //transform.Translate(214, 256, 0);// image lenght 428x512
            transform.Translate(0, 75, 0);// image lenght 428x512

            vtkTransformPolyDataFilter polylineTransform = new vtkTransformPolyDataFilter();
            polylineTransform.SetInputData(polyData);
            polylineTransform.SetTransform(transform);
            polylineTransform.Update();

            vtkPolyData curve = polylineTransform.GetOutput();
            return curve;
        }

        private void displayAllSkin()
        {
            int sliceNumber = dicomSlices.Count;
            
            for (int i =sliceNumber;i>0;i--)
            {
                
                var polylineTest = makePolyLine(i);

                vtkPolyDataMapper curveMapper = new vtkPolyDataMapper();
                curveMapper.SetInputData(polylineTest);

                vtkActor curveActor = new vtkActor();
                curveActor.SetMapper(curveMapper);
                renderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer().AddActor(curveActor);
            }
        }

        private vtkImageData appendImages()
        {
            vtkImageAppend imageAppend = new vtkImageAppend();

            for(int i=dicomSlices.Count()-1;i>-1;i--)
            {
                imageAppend.AddInputData(dicomSlices[i].croppedImageData);
            }
            imageAppend.SetAppendAxis(2);
            imageAppend.Update();

            vtkImageData croppedStudy = imageAppend.GetOutput();
            imageAppend.Dispose();
            return croppedStudy;
        }
        /// <summary>
        /// Diplay by Keys
        /// </summary>
        #region displayCroppedDicom
        vtkImageViewer2 _ImageViewer;
        int _Slice;
        int _MinSlice;
        int _MaxSlice;
        private void displayCroppedDicoms(vtkImageData removeTable)
        {
            _ImageViewer = new vtkImageViewer2();
            
            //displayControl(_MinSlice);


            _ImageViewer.SetRenderWindow(renderWindowControl.RenderWindow);
            _ImageViewer.SetInputData(removeTable);
            _ImageViewer.GetSliceRange(ref _MinSlice, ref _MaxSlice);
            _Slice = _MinSlice;
            _ImageViewer.SetSlice(_MinSlice);
            _ImageViewer.Render();


            //displayAllSkin();
            //foreach(var ds in dicomSlices)
            //{
            //    vtkPolyDataMapper curveMapper = new vtkPolyDataMapper();
            //    curveMapper.SetInputData(ds.polyLine);

            //    vtkActor curveActor = new vtkActor();
            //    curveActor.SetMapper(curveMapper);
            //    renderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer().AddActor(curveActor);

            //    vtkImageActor imagedata = new vtkImageActor();
            //    imagedata.SetInputData(ds.croppedImageData);
            //    renderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer().AddActor(imagedata);
            //   /* renderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer().Render();
            //    renderWindowControl.RenderWindow.Render();*/

            //    //Thread.Sleep(50);
            //}

        }
        private void displayControl(int slice)
        {
            vtkPolyDataMapper curveMapper = new vtkPolyDataMapper();
            curveMapper.SetInputData(dicomSlices[slice].polyLine);

            vtkActor curveActor = new vtkActor();
            curveActor.SetMapper(curveMapper);
            renderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer().AddActor(curveActor);

            vtkImageActor imagedata = new vtkImageActor();
            imagedata.SetInputData(dicomSlices[slice].croppedImageData);
            renderWindowControl.RenderWindow.GetRenderers().GetFirstRenderer().AddActor(imagedata);
        }
        /// <summary>
        /// move forward to next slice
        /// </summary>
        private void MoveForwardSlice()
        {
            if (_Slice < _MaxSlice)
            {
                _Slice += 1;
                //displayControl(_Slice);
                _ImageViewer.SetSlice(_Slice);
                _ImageViewer.Render();
            }
        }


        /// <summary>
        /// move backward to next slice
        /// </summary>
        private void MoveBackwardSlice()
        {
            if (_Slice > _MinSlice)
            {
                _Slice -= 1;
                //displayControl(_Slice);
                _ImageViewer.SetSlice(_Slice);
                _ImageViewer.Render();
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                MoveBackwardSlice();
                
            }
        }

        private void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up)
            {
                MoveForwardSlice();
            }
        }

        #endregion
        private void test()
        {
            //displayAllSkin();

            //--------cutting dicom directory collectivelly-------
            //allCurves();
            /*vtkImageData originalImageData = reader.GetOutput();
            originalImageData.SetOrigin(i[0], i[1], i[2] + (5 * 75));
            vtkImageData imageData = cuttingAllDicomes(originalImageData, allCurves());*/
            //----------view test all------------
            /*vtkPolyDataMapper mapper = new vtkPolyDataMapper();
            mapper.SetInputData(allCurves());

            vtkActor actorpoly = new();
            actorpoly.SetMapper(mapper);

            vtkImageActor actor = new();
            //actor.SetInputData(originalImageData);
            //actor.SetInputData(imageData);

            renderWindow.GetRenderers().GetFirstRenderer().AddActor(actor);
            renderWindow.GetRenderers().GetFirstRenderer().AddActor(actorpoly);
            renderWindow.GetRenderers().GetFirstRenderer().ResetCamera();*/
            //--------view test one by one-----------
            /*int sliceNumber = 12;
            var polylineTest = makePolyLine(sliceNumber);*/

            //-----To sure image data set corrected
            /*vtkPolyDataMapper curveMapper = new vtkPolyDataMapper();
            curveMapper.SetInputData(polylineTest);

            vtkActor curveActor = new vtkActor();
            curveActor.SetMapper(curveMapper);
            renderWindow.GetRenderers().GetFirstRenderer().AddActor(curveActor);

            vtkImageActor imageActor = new vtkImageActor();
            imageActor.SetInputData(dicomSlices.First(slice => slice.sliceId.Equals(sliceNumber)).imageData);
            renderWindow.GetRenderers().GetFirstRenderer().AddActor(imageActor);*/
        }
    }
}