using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using SuperMap.Desktop;
using SuperMap.Data;

namespace DesktopPlugin1
{
    /**
     * 1.现实生活中，我们常采用以沿线要素距离的方式定位，这比传统的精确（X,Y）坐标定位的方式更符合人们的习惯。
     *   比如在某某路口东300米处发生交通事故，比描述为发生在（6570.3876,3589.6082）坐标处更容易定位。 
     * 
     * 2.线性参考可用于多个属性表与线性要素的关联，不需要在属性值发生变化时分割线数据。 
     *
     */
    class MyCtrlAction : CtrlAction
    {
        private String workspaceName = "Route";
        private String datasourceName = "Route";
        private String datasetName = "Routes";
        private Dataset currentDataset = null;
        private Datasource currentDatasource = null;

        private string tempDatasetName="";
        private List<Vehicle> vehicleList = new List<Vehicle>();

        override public void Run()
        {
            //To do your work.

            //获取当前应用程序中被操作的数据集数组
            currentDataset = SuperMap.Desktop.Application.ActiveDatasets[0];
            currentDatasource = SuperMap.Desktop.Application.ActiveDatasources[0];
            //在地图窗口中输出被操作的数据集的名称
            this.MyLog(currentDataset.Name);

            
            // Routes
            Workspace workspace=SuperMap.Desktop.Application.ActiveApplication.Workspace;
            DatasetVector routesDataset = workspace.Datasources[this.datasourceName].Datasets[this.datasetName] as DatasetVector;
            // 数据处理
            DataHandling(currentDataset, routesDataset);

            //creat new dataset
            //CopyToNewDataset(vehicleList);

        }
        // 通过线性参考 处理数据
        private void DataHandling(Dataset currentDs, DatasetVector routesDataset)
        {

            if(currentDs.Type==DatasetType.Tabular){
                SuperMap.Desktop.Application.ActiveApplication.Output.Output("DatasetType.Tabular");
            }

            DatasetVector datasetVector=currentDs as DatasetVector;
            Recordset rd = datasetVector.GetRecordset(false, CursorType.Dynamic);
            Recordset routeRd = routesDataset.GetRecordset(false, CursorType.Dynamic);

            
            rd.MoveFirst();
            while (!rd.IsEOF)
            {
                String _id = rd.GetFieldValue("ID").ToString();
                String _name = rd.GetFieldValue("NAME").ToString();
                String _xbh = rd.GetFieldValue("XBH").ToString();
                String _licheng = rd.GetFieldValue("LICHENG").ToString();
                String _x = rd.GetFieldValue("X").ToString();
                String _y = rd.GetFieldValue("Y").ToString();



                //质量差的数据过滤
                if (_id.Trim() == "" || _xbh.Trim() == "" || _licheng.Trim() == "") {
                    rd.MoveNext();
                    continue;
                }

                // 如果有xy坐标，使用xy坐标；如果没有，使用线性参考获得坐标
                if (_x.Trim() != "" && _y.Trim() != "")
                {
                    //使用xy
                    try
                    {
                        double x=0, y=0;
                        double.TryParse(_x, out x);
                        double.TryParse(_y, out y);

                        //add list
                        Vehicle vehicle = new Vehicle();
                        vehicle.Id = _id;
                        vehicle.Name = _name;
                        vehicle.X = x;
                        vehicle.Y = y;
                        vehicle.Xbh = _xbh;
                        vehicleList.Add(vehicle);

                    }
                    catch (Exception)
                    {

                        this.MyLog("经纬度坐标格式有误，请使用如下格式：111.67,23.89");
                    }
                }
                else {
                    //使用 线性参考
                    GeoLineM geoLineM = new GeoLineM();

                    //游标移到开头
                    routeRd.MoveFirst();
                    while (!routeRd.IsEOF)
                    {
                        String xbh = routeRd.GetFieldValue("线编号").ToString();
                        if (xbh == _xbh)
                        {

                            geoLineM = (GeoLineM)routeRd.GetGeometry();
                            double mValue = 0;
							//解析里程值
                            mValue = GetMValue(_licheng);
                            Point2D point = geoLineM.GetPointAtM(mValue);
                            this.MyLog("坐标:" + point.X + "," + point.Y);
                            rd.SetFieldValue("X", point.X.ToString());
                            rd.SetFieldValue("Y", point.Y.ToString());

                            //add list
                            Vehicle vehicle = new Vehicle();
                            vehicle.Id = _id;
                            vehicle.Name = _name;
                            vehicle.X = point.X;
                            vehicle.Y = point.Y;
                            vehicle.Xbh = _xbh;
                            vehicleList.Add(vehicle);
                            break;
                        }
                        //
                        routeRd.MoveNext();
                    }

                }//---else end

                rd.MoveNext();
            
            }
            //update
            //rd.Update();

            rd.Close();
            rd.Dispose();
            routeRd.Close();
            routeRd.Dispose();

            this.MyLog("rd---------end");
			
            //creat new dataset
            //处理后的数据 存储在新建要素集
            CopyToNewDataset(vehicleList);
        }

        //处理后的数据 存储在新建要素集
        private void CopyToNewDataset(List<Vehicle> vehicleList){
            this.MyLog(vehicleList.Count.ToString());

            DatasetVector dataset = DatasetVectorInfoSample(this.currentDatasource, this.currentDataset.Name);

            if (!dataset.IsOpen) {

                dataset.Open();
                this.MyLog(dataset.FieldCount.ToString());
            } 

            Recordset rd = dataset.GetRecordset(false, CursorType.Dynamic);

            foreach (Vehicle v in vehicleList)
            {
                Dictionary<string, object> dic = new Dictionary<string, object>();
                dic.Add("ID", v.Id);
                dic.Add("NAME", v.Name);
                dic.Add("XBH", v.Xbh);
                dic.Add("X", v.X);
                dic.Add("Y", v.Y);
                GeoPoint point = new GeoPoint(v.X, v.Y);

                rd.AddNew(point,dic);

                rd.Update();

            }


            rd.Close();
            rd.Dispose();
            

        }

        // 新建要素集
        public DatasetVector DatasetVectorInfoSample(Datasource datasource, string tableName)
        {
            try
            {
                this.tempDatasetName = datasource.Datasets.GetAvailableDatasetName(tableName);

                // 设置矢量数据集的信息
                DatasetVectorInfo datasetVectorInfo = new DatasetVectorInfo();
                datasetVectorInfo.Type = DatasetType.Point;
                datasetVectorInfo.IsFileCache = true;
                datasetVectorInfo.Name = tempDatasetName;
                this.MyLog("矢量数据集的信息为：" + datasetVectorInfo.ToString());

                // 创建矢量数据集
                DatasetVector ds = datasource.Datasets.Create(datasetVectorInfo);

                //add field info
                FieldInfos fields = ds.FieldInfos;
                FieldInfo fieldInfo = new FieldInfo("ID", FieldType.Text);
                fields.Add(fieldInfo);
                fieldInfo = new FieldInfo("NAME", FieldType.Text);
                fields.Add(fieldInfo);
                fieldInfo = new FieldInfo("XBH", FieldType.Text);
                fields.Add(fieldInfo);

                fieldInfo = new FieldInfo("X", FieldType.Double);
                fields.Add(fieldInfo);
                fieldInfo = new FieldInfo("Y", FieldType.Double);
                fields.Add(fieldInfo);

                ds.Close();

                return ds;

            }
            catch (Exception)
            {

                return null;
            }


        }

        // 里程数据解析
        private double GetMValue(string licheng) {
            this.MyLog(licheng);
            double m = 0;
            int indexMiddle = licheng.IndexOf('+');
            String kmValue = licheng.Substring(1, indexMiddle-1);
            string mValue = licheng.Substring(indexMiddle + 1);
            if (mValue.IndexOf('-') > -1) {

                int indexTemp = mValue.IndexOf('-');
                mValue = mValue.Substring(0, indexTemp);
            }

            //m = int.Parse(kmValue) * 1000 + int.Parse(mValue);
            m = double.Parse(kmValue) + double.Parse(mValue) / 1000;

            this.MyLog(m.ToString());
            return m;
        }

        // 日志输出
        private void MyLog(string s) {
            SuperMap.Desktop.Application.ActiveApplication.Output.Output(s);

        }


        //todo


        //inner class
        class Vehicle
        {

            private string id;

            public string Id
            {
                get { return id; }
                set { id = value; }
            }
            private string name;

            public string Name
            {
                get { return name; }
                set { name = value; }
            }
            private double x;

            public double X
            {
                get { return x; }
                set { x = value; }
            }


            private double y;

            public double Y
            {
                get { return y; }
                set { y = value; }
            }



            private string xbh;

            public string Xbh
            {
                get { return xbh; }
                set { xbh = value; }
            }
        }
    }
}
