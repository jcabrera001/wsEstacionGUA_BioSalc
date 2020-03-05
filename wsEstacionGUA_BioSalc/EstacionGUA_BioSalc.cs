using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using RestSharp;
using System.Data.SqlClient;
using Newtonsoft.Json;
using System.Timers;
using Newtonsoft.Json.Linq;

namespace wsEstacionGUA_BioSalc
{
    partial class EstacionGUA_BioSalc : ServiceBase
    {      
        private static Timer aTimer;
        private SqlDataAdapter adpClima = new SqlDataAdapter();
        private DataTable dtClima = new DataTable();
        private SqlDataAdapter adp = new SqlDataAdapter();
        private DataTable dt = new DataTable();
        private SqlCommand cnn;
        SqlConnection cnx = new SqlConnection("Persist Security Info = False; User ID = servicios; Password = Service.1380; Initial Catalog = Biosalc; Server = 10.1.1.6\\amigodb");

        public EstacionGUA_BioSalc()
        {
            InitializeComponent();
            if (!System.Diagnostics.EventLog.SourceExists("MySource"))
            {
                System.Diagnostics.EventLog.CreateEventSource("MySource", "MyNewLog");
            }
            eventLog1.Source = "MySource";
            eventLog1.Log = "MyNewLog";
           
            eventLog1.WriteEntry("Inicie");
            aTimer = new Timer();
            aTimer.Interval = 86400000;  //Valor en milisengudos( Equivalente a 24 horas).
            aTimer.Elapsed += new ElapsedEventHandler(aTimerElapsed);
        }

        protected override void OnStart(string[] args)
        {
            // TODO: Add code here to start your service.
            aTimer.Enabled = true;
        }

        public void aTimerElapsed(object sender, EventArgs e)
        {
            Integracion();
        }

        protected override void OnStop()
        {
            // TODO: Add code here to perform any tear-down necessary to stop your service.
            eventLog1.WriteEntry("Final");
            aTimer.Enabled = false;
        }

        //https://www.newtonsoft.com/json/help/html/QueryingLINQtoJSON.htm
        public void Integracion()
        {
            dtClima = new DataTable();
            adpClima = new SqlDataAdapter("select  Precipitacao pre, Tminima tmin, tmaxima tmax, UMIDADE Umi, " +
                             "umimax, Veloc vel, VEloc_Max VelMax, Radiacao Rad, evapo_trans eva, humidade_med hmed, Horas_Sol hsol, Horas_Luz hluz " +
                             "from ClimaTest where data = ''", cnx);
            adpClima.InsertCommand = new SqlCommand("spGUAEstacionInsert", cnx);
            adpClima.InsertCommand.CommandType = CommandType.StoredProcedure;
            adpClima.InsertCommand.Parameters.Add("pre", SqlDbType.Float, 40, "pre");
            adpClima.InsertCommand.Parameters.Add("tmax", SqlDbType.Float, 40, "tmax");
            adpClima.InsertCommand.Parameters.Add("tmin", SqlDbType.Float, 40, "tmin");
            adpClima.InsertCommand.Parameters.Add("umi", SqlDbType.Float, 40, "umi");
            adpClima.InsertCommand.Parameters.Add("umimax", SqlDbType.Float, 40, "umimax");
            adpClima.InsertCommand.Parameters.Add("vel", SqlDbType.Float, 40, "vel");
            adpClima.InsertCommand.Parameters.Add("velMax", SqlDbType.Float, 40, "velMax");
            adpClima.InsertCommand.Parameters.Add("rad", SqlDbType.Float, 40, "rad");
            adpClima.InsertCommand.Parameters.Add("eva", SqlDbType.Float, 40, "eva");
            adpClima.InsertCommand.Parameters.Add("hsol", SqlDbType.Float, 40, "hsol");
            adpClima.InsertCommand.Parameters.Add("hluz", SqlDbType.VarChar, 40, "hluz");
            adpClima.InsertCommand.Parameters.Add("hmed", SqlDbType.Float, 40, "hmed");

            adpClima.Fill(dtClima);
            dtClima.Rows.Add();

            var client = new RestClient("http://www.wwf-mar.org:8080/addUPI?function=login&user=cahsait&passwd=cahsa2017!&timeout=1800&asJson=true");//Dirección URL de la página.    
            var request = new RestRequest();//, Method.GET);       // Solicitud por método POST, con los párametros.

            request.RequestFormat = DataFormat.Json;            //Convirtiendo la información a formato JSON.
            IRestResponse response = client.Execute(request);   //Ejecutando el Request.
            var content = response.Content;                     //Almacenando el contenido del Request.

            try { 
                JObject rss = JObject.Parse(content.ToString());
                string sessionID = (string)rss["result"]["string"];


                Data(sessionID, "3091", "pre"); //Precipitación.
                Data(sessionID, "7198", "tmax"); // Temperatura máxima.
                Data(sessionID, "7197", "tmin"); //Temperatura mínima.
                //Data(sessionID, "7196", "tme"); //Temperatura promedio.
                Data(sessionID, "7201", "umi"); //Humedad Relativa.
                Data(sessionID, "7202", "umimax"); // Humedad máxima.         
                Data(sessionID, "7200", "hmed"); // Humedad relativa AVG.    
                Data(sessionID, "7209", "vel"); //Velocidad.
                Data(sessionID, "7210", "velMax"); //Velocidad máxima.
                Data(sessionID, "4618", "rad"); //Radiacion. () Real 4661
                Data(sessionID, "3044", "eva"); //EvapoTranspiración.    
                Data(sessionID, "4616", "hsol"); // Horas Sol. (Real 6700)
                Data(sessionID, "6694", "hluz"); //Horas luz.   (Real 4660)
                Insert(sessionID); //Ejecutar la insercción y hacer el Loguot del API.
            }
            catch(Exception ex)
            {
                cnx.Open();
                cnn = new SqlCommand("spBitacoraInsert @err, @app", cnx);
                cnn.Parameters.AddWithValue("@err", response.StatusDescription + "   " + ex.Message + "   " + DateTime.Now.ToLongTimeString());
                cnn.Parameters.AddWithValue("@app", "EstacionGUA_BioSalc");
                cnn.ExecuteNonQuery();
                cnx.Close();
            }
        }


        public void Data(string sessionID, string nodo, string column)
        {
            string fecha;
            dt = new DataTable();
            adp = new SqlDataAdapter("select * from vFechaEstacion", cnx);
            adp.Fill(dt);

            fecha = dt.DefaultView[0]["ayer"].ToString();

            if (nodo == "3091")
            {
                fecha = dt.DefaultView[0]["hoy"].ToString();
            }

            string url = "http://www.wwf-mar.org:8080/addUPI?function=getdata&session-id=" + sessionID + "&id=" + nodo + "&asJson=true&date=" + fecha;
            var client = new RestClient(url);                    //Dirección URL de la página.
                var request = new RestRequest();    // Solicitud por método GET, con los párametros por el Header.

                // execute the request
                request.RequestFormat = DataFormat.Json;                //Convirtiendo el la información a formato JSON.
                IRestResponse response = client.Execute(request);       //Ejecutando el Request.
                var content = response.Content;                         //Almacenando el contenido del Request.
            try
            {
                JObject rss = JObject.Parse(content.ToString());        //Convirtiendo el resulta a un "JObject"
                JToken value = rss["node"][0]["v"][0]["value"];        // Accediendo al campo "value" (Valor del nodo requerido).

                dtClima.DefaultView[0][column] = value;               
            }
            catch (Exception ex)
            {
                cnx.Open();
                cnn = new SqlCommand("spBitacoraInsert @err, @app", cnx);
                cnn.Parameters.AddWithValue("@err", response.StatusDescription + ", " + ex.Message + "   " + DateTime.Now.ToLongTimeString());
                cnn.Parameters.AddWithValue("@app", "EstacionGUA_BioSalc");
                cnn.ExecuteNonQuery();
                cnx.Close();
               
            }
        }

        public void Insert(string sessionID)
        {
            try
            {
                adpClima.Update(dtClima); //Ingreso de la información.
            }
            catch (Exception ex)
            {

                cnx.Open();
                cnn = new SqlCommand("spBitacoraInsert @err, @app", cnx);
                cnn.Parameters.AddWithValue("@err", ex.Message);
                cnn.Parameters.AddWithValue("@app", "EstacionGUA_BioSalc");
                cnn.ExecuteNonQuery();
                cnx.Close();
            }
             
            try
            {
                string url = "http://www.wwf-mar.org:8080/addUPI?function=logout&session-id=" + sessionID;
                var client = new RestClient(url);    //Dirección URL de la página.
                var request = new RestRequest();    // Solicitud por método GET, con los párametros por el Header.

                // execute the request
                IRestResponse response = client.Execute(request); //Ejecutando el Request, para cerrar la conexion existente.
            }
            catch(Exception ex)
            {
                cnx.Open();
                cnn = new SqlCommand("spBitacoraInsert @err, @app", cnx);
                cnn.Parameters.AddWithValue("@err", ex.Message);
                cnn.Parameters.AddWithValue("@app", "EstacionGUA_BioSalc");
                cnn.ExecuteNonQuery();
                cnx.Close();
            }
        }

        //https://www.newtonsoft.com/json/help/html/SerializeObject.htm

    }
}
