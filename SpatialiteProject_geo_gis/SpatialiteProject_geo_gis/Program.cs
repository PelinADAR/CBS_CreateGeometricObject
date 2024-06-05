using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Reflection.PortableExecutable;
using System.Runtime.Serialization.Formatters.Binary;

namespace SpatialiteProject_geo_gis
{


    public class Program
    {
        private static string connectionString = "Data Source=template.sqlite;Version=3;";


        public static void Main()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                connection.EnableExtensions(true);
                connection.LoadExtension("mod_spatialite");



                while (true)
                {
                    Console.WriteLine("1. Building objelerini oluştur");
                    Console.WriteLine("2. Kapı konumlarını analiz et");
                    Console.WriteLine("3. Kapı sonuçlarını yazdır");


                    Console.Write("Seçiminizi girin: ");
                    string choice = Console.ReadLine();

                    switch (choice)
                    {
                        case "1":
                            CreateBuildingObjects(connection);
                            break;
                        case "2":
                            AnalyzeDoorLocations(connection);
                            break;
                        case "3":
                            PrintDoorResults(connection);
                            break;
                        default:
                            Console.WriteLine("Geçersiz seçim. Tekrar deneyin.");
                            break;
                    }
                }
            }


            static void CreateBuildingObjects(SQLiteConnection connection)
            {


                List<int> building_ids = new List<int>();

                    
                using (SQLiteCommand command = new SQLiteCommand("SELECT building_id FROM building_nodes group by building_id", connection))
                {
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        
                        while (reader.Read())
                        {

                            int building_id = reader.GetInt32(reader.GetOrdinal("building_id"));
                            building_ids.Add(building_id);
                        }

                    }
                }
                var controlSelectCommand = new SQLiteCommand("SELECT * FROM building", connection);
                var controlSelectResult = controlSelectCommand.ExecuteReader();
                if (controlSelectResult.HasRows)
                {

                    string delete_sql = "DELETE FROM building;";

                    using (var command = new SQLiteCommand(delete_sql, connection))
                    {
                        command.CommandText = delete_sql;
                        var stats = command.ExecuteNonQuery();

                    }
                }


                    foreach (var building in building_ids)
                    {
                        


                       
                            string sql = "INSERT INTO building (id, geom) SELECT building_id, ST_ConvexHull(ST_Collect(geom)) FROM building_nodes  WHERE building_id=" + building;





                            using (var command = new SQLiteCommand(sql, connection))
                            {
                                command.CommandText = sql;
                                //Console.WriteLine($"Çalıştırılacak SQL: {sql}");
                                var stats = command.ExecuteNonQuery();

                            }
                        


                    }
                    
                Console.WriteLine("-------------------------------------------------------------------");
                Console.WriteLine("-------------------------------------------------------------------");
                Console.WriteLine("Bina Geometrik objeleri oluşturulup building tablosuna eklenmiştir.");
                Console.WriteLine("-------------------------------------------------------------------");
                Console.WriteLine("-------------------------------------------------------------------");




            }


            static void AnalyzeDoorLocations(SQLiteConnection connection)
            {

                var cmdSelect = new SQLiteCommand("SELECT id, building_id FROM door", connection);
                var reader = cmdSelect.ExecuteReader();
                var doors = new List<(int id, int building_id)>();

                while (reader.Read())
                {
                    doors.Add((reader.GetInt32(0), reader.GetInt32(1)));
                }
                reader.Close();

                foreach (var door in doors)
                {
                    string queryCheckBuilding = $"SELECT id FROM building WHERE id = {door.building_id}";
                    var cmdCheckBuilding = new SQLiteCommand(queryCheckBuilding, connection);
                    var buildingIdObj = cmdCheckBuilding.ExecuteScalar();

                    if (buildingIdObj != null)
                    {

                        string querySpatialCheck = $@"
                    SELECT ST_Within(
                        (SELECT geom FROM door WHERE building_id = {door.building_id}),
                        (SELECT geom FROM building WHERE id = {door.building_id})
                    )";
                        var cmdSpatialCheck = new SQLiteCommand(querySpatialCheck, connection);
                        var isInsideObj = cmdSpatialCheck.ExecuteScalar();
                        bool isInside = isInsideObj != DBNull.Value && Convert.ToBoolean(isInsideObj);

                        string queryUpdate = $"UPDATE door SET inside_building = {(isInside ? 1 : 0)} WHERE id = {door.id}";
                        var cmdUpdate = new SQLiteCommand(queryUpdate, connection);
                        cmdUpdate.ExecuteNonQuery();
                    }
                    else
                    {
                        string queryUpdateNull = $"UPDATE door SET inside_building = NULL WHERE id = {door.id}";
                        var cmdUpdateNull = new SQLiteCommand(queryUpdateNull, connection);
                        cmdUpdateNull.ExecuteNonQuery();
                    }

                }
                Console.WriteLine("--------------------------------------------------------------------");
                Console.WriteLine("--------------------------------------------------------------------");
                Console.WriteLine("Kapı konumlarının bina içerisinde olup olmadığı analizi yapılmıştır.");
                Console.WriteLine("--------------------------------------------------------------------");
                Console.WriteLine("--------------------------------------------------------------------");


            }

            static void PrintDoorResults(SQLiteConnection connection)
            {
                using (var command = new SQLiteCommand("SELECT id, building_id, door_no, inside_building FROM door", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = reader.GetInt32(0);
                            int building_id = reader.GetInt32(1);
                            string door_no = reader.GetString(2);
                            int? inside_building = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);

                            if (inside_building == 1)
                                Console.WriteLine($"Kapı id: {id}, Bina id: {building_id}, Kapı No: {door_no}, Bina İçinde Mi: Evet");
                            else if (inside_building == 0)
                                Console.WriteLine($"Kapı id: {id}, Bina id: {building_id}, Kapı No: {door_no}, Bina İçinde Mi: Hayır");
                            else
                                Console.WriteLine($"Kapı id: {id}, Bina id: {building_id}, Kapı No: {door_no}, Bina İçinde Mi: Tespit Edilemedi");
                        }
                    }
                }
            }




        }
    }
}
