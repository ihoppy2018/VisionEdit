﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonMethods;
using HalconDotNet;
using HalconWindow.HalconWindow;
using ToolBase;
using static DataStruct.DataStruct;

namespace CaliperTool
{
    [Serializable]
    public class Caliper: IToolBase
    {
        public bool toolEnable = true;
        /// <summary>
        /// 输入姿态
        /// </summary>
        public PosXYU inputPose = new PosXYU();
        /// <summary>
        /// 制作模板时的输入位姿
        /// </summary>
        public PosXYU templatePose = new PosXYU();
        /// <summary>
        /// 卡尺
        /// </summary>
        public HObject contoursDisp = null;
        /// <summary>
        /// 箭头
        /// </summary>
        public HObject arrowDisp = null;
        /// <summary>
        /// 交点
        /// </summary>
        public HObject crossDisp = null;
        /// <summary>
        /// 期望矩形中心行坐标
        /// </summary>
        public HTuple expectRecStartRow = 200;
        /// <summary>
        /// 期望矩形中心列坐标
        /// </summary>
        public HTuple expectRecStartColumn = 200;
        /// <summary>
        /// 期望矩形起点方向
        /// </summary>
        public HTuple expectAngle = 0;
        /// <summary>
        /// 卡尺高
        /// </summary>
        public HTuple length1 = 40;
        /// <summary>
        /// 卡尺宽
        /// </summary>
        public HTuple length2 = 40;
        /// <summary>
        /// 找边极性，从明到暗或从暗到明
        /// </summary>
        public string polarity = "negative";
        /// <summary>
        /// 边阈值
        /// </summary>
        public int threshold = 30;
        /// <summary>
        /// 边Sigma
        /// </summary>
        public double sigma = 1.0;
        /// <summary>
        /// 选择所查找到的点
        /// </summary>
        public string edgeSelect = "all";
        /// <summary>
        /// 矩形框显示
        /// </summary>
        public bool dispRec = true;
        /// <summary>
        /// 交点显示
        /// </summary>
        public bool dispCross = true;
        /// <summary
        /// 是否显示的线
        /// </summary>
        public bool LineDisp = true;
        /// <summary>
        /// 新的跟随姿态变化后的预期线信息
        /// </summary>
        HTuple newExpectRecStartRow = new HTuple(200), newExpectRecStartColumn = new HTuple(200), newExpectPhi = new HTuple(0);
        /// <summary>
        /// 查找到的线的起点行坐标
        /// </summary>
        private HTuple _resultRow = 0;
        public HTuple ResulttRow
        {
            get
            {
                _resultRow = Math.Round((double)_resultRow, 3);
                return _resultRow;
            }
            set { _resultRow = value; }
        }
        /// <summary>
        /// 查找到的线的起点列坐标
        /// </summary>
        private HTuple _resultCol = 0;
        public HTuple ResultCol
        {
            get
            {
                _resultCol = Math.Round((double)_resultCol, 3);
                return _resultCol;
            }
            set { _resultCol = value; }
        }

        public HObject inputImage { get; set; } = null;

        public ToolRunStatu toolRunStatu { get; set; } = ToolRunStatu.Not_Run;

        public SoftwareRunState softwareRunState { get; set; } = SoftwareRunState.Debug;
       

        public void DispImage()
        {
            if (inputImage != null)
            {
                FormCaliper.Instance.myHwindow.HobjectToHimage(inputImage);
            }
        }

        internal void DrawExpectLine(HWindow_Final myHwindow)
        {
            if (inputImage != null)
            {
                try
                {
                    myHwindow.DrawModel = true;
                    myHwindow.Focus();
                    HOperatorSet.SetColor(myHwindow.hWindowControl.HalconWindow, new HTuple("green"));
                    HOperatorSet.DrawRectangle2Mod(myHwindow.hWindowControl.HalconWindow, expectRecStartRow, expectRecStartColumn, expectAngle, length1, length2,
                        out expectRecStartRow, out expectRecStartColumn, out expectAngle, out length1, out length2);

                    if (inputPose != null)
                    {
                        templatePose.X = inputPose.X;
                        templatePose.Y = inputPose.Y;
                        templatePose.U = inputPose.U;
                    }
                    // 输入
                    FormCaliper.Instance.tbx_expectCenterRow.Text = expectRecStartRow.TupleString("10.3f");
                    FormCaliper.Instance.tbx_expectCenterCol.Text = expectRecStartColumn.TupleString("10.3f");
                    FormCaliper.Instance.tbx_expectPhi.Text = expectAngle.TupleString("10.3f");
                    // 参数
                    FormCaliper.Instance.tbx_caliperLength1.Text = length1.TupleString("10.3f");
                    FormCaliper.Instance.tbx_caliperLength2.Text = length2.TupleString("10.3f");

                    myHwindow.DrawModel = false;

                   // Run();
                }
                catch (Exception ex)
                {
                    FormCaliper.Instance.TextBoxMessageDisp(ex.Message, System.Drawing.Color.Red);
                }
            }
            else
            {
                FormCaliper.Instance.TextBoxMessageDisp("图像为空", System.Drawing.Color.Red);
            }

        }

        public void UpdateImage()
        {
            FormCaliper.Instance.myHwindow.ClearWindow();
            DispImage();
        }

        public void Run(SoftwareRunState softwareRunState)
        {
            HTuple HMeasureHandle = new HTuple();
            HTuple resultRow, resultCol;
            if (inputImage == null)
            {
                if(softwareRunState == SoftwareRunState.Debug)
                {
                    FormCaliper.Instance.TextBoxMessageDisp("图像为空", System.Drawing.Color.Red);
                }
                toolRunStatu = ToolRunStatu.Not_Input_Image;
                return;
            }
            try
            {
                UpdateImage();
                if (inputPose != null)
                {
                    HTuple Row = inputPose.X - templatePose.X;
                    HTuple Col = inputPose.Y - templatePose.Y;
                    HTuple angle = inputPose.U - templatePose.U;

                    HTuple _homMat2D;
                    HOperatorSet.HomMat2dIdentity(out _homMat2D);
                    HOperatorSet.HomMat2dRotate(_homMat2D, (HTuple)(angle), (HTuple)templatePose.X, (HTuple)templatePose.Y, out _homMat2D);
                    HOperatorSet.HomMat2dTranslate(_homMat2D, (HTuple)(Row), (HTuple)(Col), out _homMat2D);

                    //对预期线的起始点做放射变换
                    HOperatorSet.AffineTransPixel(_homMat2D, (HTuple)expectRecStartRow, (HTuple)expectRecStartColumn, out newExpectRecStartRow, out newExpectRecStartColumn);
                }
                else
                {
                    newExpectRecStartRow = expectRecStartRow;
                    newExpectRecStartColumn = expectRecStartColumn;
                }
                HTuple width, height, AmplitudeThreshold, distance;
                HOperatorSet.GetImageSize(inputImage, out width, out height);
                HOperatorSet.GenMeasureRectangle2(expectRecStartRow, expectRecStartColumn, expectAngle, length1, length2, width, height, "nearest_neighbor", out HMeasureHandle);
                HOperatorSet.MeasurePos(inputImage, HMeasureHandle, sigma, threshold, polarity, edgeSelect, out resultRow, out resultCol, out AmplitudeThreshold, out distance);
                if(resultRow.Length != 0)
                {
                    ResulttRow = resultRow;
                    ResultCol = resultCol;
                }
                
                //把点显示出来
                HOperatorSet.GenCrossContourXld(out crossDisp, ResulttRow, ResultCol, new HTuple(60), new HTuple(0));
                if(softwareRunState == SoftwareRunState.Debug)
                {
                    DispMainWindow(FormCaliper.Instance.myHwindow);
                    FormCaliper.Instance.tbx_resultStartRow.Text = ResulttRow.ToString();
                    FormCaliper.Instance.tbx_resultStartCol.Text = ResultCol.ToString();
                    FormCaliper.Instance.TextBoxMessageDisp("运行成功", System.Drawing.Color.Green);
                }
                // 参数传递
                ParamsTrans();
                toolRunStatu = ToolRunStatu.Succeed;
            }
            catch (Exception ex)
            {
                toolRunStatu = ToolRunStatu.Not_Succeed;
                if (softwareRunState == SoftwareRunState.Debug)
                {
                    FormCaliper.Instance.TextBoxMessageDisp("工具运行异常" + ex.Message, System.Drawing.Color.Red);
                }   
            }
            finally
            {
                //homMat2DArrow.Dispose();
                //arrow.Dispose();
                //arrowTrans.Dispose();
            }
        }

        /// <summary>
        /// 将数据传递给FindlineToolInterface
        /// </summary>
        private void ParamsTrans()
        {
            if(FormCaliper.Instance.myToolInfo != null)
            {
                FormCaliper.Instance.myToolInfo.toolOutput.Clear();
                FormCaliper.Instance.myToolInfo.toolOutput.Add(new ToolIO("outputCenterRow", ResulttRow, DataType.IntValue));
                FormCaliper.Instance.myToolInfo.toolOutput.Add(new ToolIO("outputCenterColumn", ResultCol, DataType.IntValue));
            }
            
        }

        public void DispMainWindow(HWindow_Final window)
        {
            // 显示矩形
            if (dispRec)
            {
                window.DispObj(contoursDisp, "blue");
            }
            // 显示交点
            if (dispCross)
            {
                window.DispObj(crossDisp, "yellow");
            }
            //显示找到的线
          //  window.DispObj(LineDisp, "green");
        }
    }
}
