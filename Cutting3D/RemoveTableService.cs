using EvilDICOM.CV.RT.Meta;
using Kitware.VTK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cutting3D
{
    internal class RemoveTableService
    {
        #region Properties
        private ObservableCollection<DicomSlice> DicomSlices;
        private double length;
        private int[] dimention;
        private double[] origin;
        private double[] spacing;
        private int[] extend;
        #endregion
        #region Methods
        #region Execute
        public vtkImageData Execute(vtkImageData imageData,StructureMeta structureMeta)
        {
            DicomSlices = new ObservableCollection<DicomSlice>() { };
            dimention = imageData.GetDimensions();
            origin = imageData.GetOrigin();
            spacing = imageData.GetSpacing();
            extend = imageData.GetExtent();

            if (imageData.GetBounds()[2] < 0)
            {
                length = imageData.GetBounds()[3] + imageData.GetBounds()[2];
            }
            else
            {
                length = imageData.GetBounds()[3] - imageData.GetBounds()[2];
            }

            //Get Stencils and set into dicomSlice collection
            getDicomSkins(structureMeta);
            
            //Get Dicom slices and set into dicomSlice collection
            getDicomSlices(imageData);

            //Create PolyLines and set into dicomSlice Collection
            getPolyLines();

            //Cropped dicomSlice collection
            cropImages();

            //Append Cropped images data
            vtkImageData result = appendImages(); 
            return result;
        }
        #endregion
        #region Get Dicom Skins
        private void getDicomSkins(StructureMeta structureMeta)
        {
            List<List<OpenCvSharp.Point2f>> stencils;
            int totalSlices = dimention[2];
            var skins = structureMeta;

            //count all contours
            int countContour = skins.SliceContours.Count;

            //Grouped skins by Z-position
            var groupedSkins = skins.SliceContours.GroupBy(contour => contour.Z);
            
            //Fill collection skins
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
                    DicomSlices.Add(dicomSlice);
                }
            }

        }
        #endregion
        #region Get Dicom Slices
        private void getDicomSlices(vtkImageData imageData)
        {
            //Convert dicom directory to slice
            for (int z = 0; z < dimention[2]; z++)
            {
                vtkExtractVOI extractFilter = vtkExtractVOI.New();
                extractFilter.SetInputData(imageData);
                extractFilter.SetVOI(0, dimention[0] - 1, 0, dimention[1] - 1, z, z);
                extractFilter.Update();

                vtkImageData sliceImage = extractFilter.GetOutput();
                
                var selectSlice = DicomSlices[dimention[2] - z - 1];
                selectSlice.imageData = sliceImage;
                extractFilter.Dispose();
            }
        }
        #endregion
        #region Make PolyLine
        private vtkPolyData makePolyLine(int slice)
        {
            var dicom = DicomSlices.First(s => s.sliceId.Equals(slice));
            var skinData = dicom.skins;
            int startPoint = 0;
            int totalPoint = 0;
            vtkPoints curvePoints = new vtkPoints();
            vtkCellArray curveCells = new vtkCellArray();
            foreach (var skin in skinData)
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
            vtkTransform transform = vtkTransform.New();
            transform.Translate(0, length, 0);
            

            vtkTransformPolyDataFilter polylineTransform = new vtkTransformPolyDataFilter();
            polylineTransform.SetInputData(polyData);
            polylineTransform.SetTransform(transform);
            polylineTransform.Update();

            vtkPolyData curve = polylineTransform.GetOutput();

            curvePoints.Dispose();
            curveCells.Dispose();
            polyData.Dispose();
            transform.Dispose();
            polylineTransform.Dispose();
            return curve;
        }
        #endregion
        #region Get PolyLines
        private void getPolyLines()
        {
            int sliceNumber = DicomSlices.Count();

            for (int i = 1; i <= sliceNumber; i++)
            {

                var polylineTest = makePolyLine(i);
                DicomSlices.First(slice => slice.sliceId.Equals(i)).polyLine = polylineTest;

            }
        }
        #endregion
        #region Crop Images OneByOne 
        private void cropImages()
        {
            foreach (var ds in DicomSlices)
            {
                // Voxelization: Convert polyline to image stencil
                vtkPolyDataToImageStencil polyToStencil = vtkPolyDataToImageStencil.New();
                polyToStencil.SetInputData(ds.polyLine);
                polyToStencil.SetOutputOrigin(origin[0], origin[1], origin[2]);
                polyToStencil.SetOutputSpacing(spacing[0], spacing[1], spacing[2]);
                polyToStencil.SetOutputWholeExtent(extend[0], extend[1], extend[2], extend[3], extend[4], extend[5]);
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

                polyToStencil.Dispose();
                stencilData.Dispose();
                imageStencil.Dispose();
            }
        }
        #endregion
        #region Append DataImages
        private vtkImageData appendImages()
        {
            vtkImageAppend imageAppend = new vtkImageAppend();

            for (int i = DicomSlices.Count() - 1; i > -1; i--)
            {
                imageAppend.AddInputData(DicomSlices[i].croppedImageData);
            }
            imageAppend.SetAppendAxis(2);
            imageAppend.Update();

            vtkImageData croppedStudy = imageAppend.GetOutput();
            imageAppend.Dispose();
            return croppedStudy;
        }
        #endregion
        #endregion
    }
}
