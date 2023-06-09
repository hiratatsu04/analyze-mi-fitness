﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Microsoft.VisualBasic.FileIO;
using System.Text.Json;
using Excel = Microsoft.Office.Interop.Excel;
using System.Runtime.InteropServices;

namespace analyze_mi_fitness
{
    public partial class AnalyzeMiFitnessExerciseData : Form
    {
        // ファイルパスを格納する
        string filePass = String.Empty;

        public AnalyzeMiFitnessExerciseData()
        {
            InitializeComponent();
        }

        private void SelectFileButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofDialog = new OpenFileDialog();

            // デフォルトのフォルダを指定する
            ofDialog.InitialDirectory = @"C:";

            //ダイアログのタイトルを指定する
            ofDialog.Title = "開くファイルを選択ください";

            //ダイアログを表示する
            if (ofDialog.ShowDialog() == DialogResult.OK)
            {
                FilePassTextBox.Text = ofDialog.FileName;
                filePass = ofDialog.FileName;
            }
            else
            {
                Console.WriteLine("キャンセルされました");
            }

            // オブジェクトを破棄する
            ofDialog.Dispose();
        }

        private void ExportButton_Click(object sender, EventArgs e)
        {
            // json 形式のデータを格納する変数を用意
            List<Dictionary<string, dynamic>> data = new List<Dictionary<string, dynamic>>();

            // ファイル名と文字エンコードを指定してパーサを実体化
            using (TextFieldParser txtParser =
                new TextFieldParser(
                        filePass,
                        Encoding.GetEncoding("utf-8")))
            {
                // 内容は区切り文字形式
                txtParser.TextFieldType = FieldType.Delimited;
                // デリミタはカンマ
                txtParser.SetDelimiters(",");

                // ファイルの終わりまで一行ずつ処理
                while (!txtParser.EndOfData)
                {
                    // 一行を読み込んで配列に結果を受け取る
                    string[] splittedResult = txtParser.ReadFields();

                    // ランニングデータのみ取得する
                    if (splittedResult[2] == "outdoor_running")
                    {
                        // splittedResult[5] の json 型データを Dictionary 型に変換
                        
                        Dictionary<string, dynamic> dic = ParseJson(splittedResult[5]);

                        // 指定の範囲のデータのみ取り出し、data に格納
                        if (dic["distance"] > Decimal.Parse(MinDistanceRangeComboBox.SelectedItem.ToString()) * 1000 && dic["distance"] < Decimal.Parse(MaxDistanceRangeComboBox.Text.ToString()) * 1000)
                        {
                            data.Add(dic);
                        }
                    }
                }
            }

            // エクセルに出力
            ExcelOutput(data);
        }

        // JSON文字列をDictionary<string, dynamic>型に変換するメソッド
        public static Dictionary<string, dynamic> ParseJson(string json)
        {
            // JSON文字列をDictionary<string, JsonData>型に変換
            Dictionary<string, JsonElement> dic = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

            // JsonElementから値を取り出してdynamic型に入れてDictionary<string, dynamic>型で返す
            return dic.ToDictionary(d => d.Key, d => JsonData(d.Value));
        }
        
        private static dynamic JsonData(JsonElement elem)
        {
            switch (elem.ValueKind)
            {
                case JsonValueKind.String:
                    return elem.GetString();
                case JsonValueKind.Number:
                    return elem.GetDecimal();
            }

            return elem;
        }

        private void ExcelOutput(List<Dictionary<string, dynamic>> data)
        {
            //Excelオブジェクトの初期化
            Excel.Application excelApp = null;
            Excel.Workbooks wbs = null;
            Excel.Workbook wb = null;
            Excel.Sheets shs = null;
            Excel.Worksheet ws = null;

            try
            {
                //Excelシートのインスタンスを作る
                excelApp = new Excel.Application();
                wbs = excelApp.Workbooks;
                wb = wbs.Add();

                shs = wb.Sheets;
                ws = shs[1];
                ws.Select(Type.Missing);

                excelApp.Visible = false;

                // タイトル行をセット
                // Excelのcell指定
                ws.Cells[1, 1] = "ランニング日時";
                ws.Cells[1, 2] = "ランニング日";
                ws.Cells[1, 3] = "平均心拍数";
                ws.Cells[1, 4] = "走行距離 [m]";
                ws.Cells[1, 5] = "所要時間 [s]";
                ws.Cells[1, 6] = "平均ペース [min]";
                ws.Cells[1, 7] = "平均ペース [min:s]";

                // エクセルファイルにデータをセットする
                for (int i = 1; i < data.Count; i++)
                {
                    ProgressLabel.Text = $"{i} / {data.Count - 1}";

                    for (int j = 1; j < 8; j++)
                    {
                        // Excelのcell指定
                        Excel.Range range = ws.Cells[i + 1, j];

                        try
                        {
                            DateTimeOffset dateTime = DateTimeOffset.FromUnixTimeSeconds(decimal.ToInt64(data[i - 1]["time"])).ToLocalTime();
                            
                            Decimal distanceKm = data[i - 1]["distance"] / 1000;
                            Decimal durationMin = data[i - 1]["duration"] / 60;
                            Decimal pace = durationMin / distanceKm;

                            int paceMinKm = decimal.ToInt32(data[i - 1]["duration"] / distanceKm) / 60;
                            int paceSecKm = decimal.ToInt32(data[i - 1]["duration"] / distanceKm) % 60;

                            // Excelにデータをセット
                            switch (j)
                            {
                                case 1:
                                    range.Value2 = dateTime.ToString();
                                    break;
                                case 2:
                                    range.Value2 = dateTime.Date.ToString();
                                    break;
                                case 3:
                                    range.Value2 = data[i - 1]["avg_hrm"];
                                    break;
                                case 4:
                                    range.Value2 = data[i - 1]["distance"];
                                    break;
                                case 5:
                                    range.Value2 = data[i - 1]["duration"];
                                    break;
                                case 6:
                                    range.Value2 = pace.ToString("0.00");
                                    break;
                                case 7:
                                    range.Value2 = $"0:{paceMinKm}:{paceSecKm}";
                                    break;
                            }
                        }
                        finally
                        {
                            // Excelのオブジェクトはループごとに開放する
                            Marshal.ReleaseComObject(range);
                            range = null;
                        }
                    }
                }

                // SaveFileDialogを表示
                SaveFileDialog saveFileDialog = new SaveFileDialog();

                //ダイアログのタイトルを指定する
                saveFileDialog.Title = "ファイルを保存する";
                // デフォルトのフォルダを指定する
                saveFileDialog.InitialDirectory = @"C:";

                // デフォルトファイル名
                saveFileDialog.FileName = @"output.xlsx";

                //ダイアログを表示する
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    wb.SaveAs(saveFileDialog.FileName);
                    wb.Close(false);
                    excelApp.Quit();
                }
                else
                {
                    Console.WriteLine("キャンセルされました");
                }

                // オブジェクトを破棄する
                saveFileDialog.Dispose();

                ProgressLabel.Text = "出力完了";
            }
            finally
            {
                //Excelのオブジェクトを開放し忘れているとプロセスが落ちないため注意
                Marshal.ReleaseComObject(ws);
                Marshal.ReleaseComObject(shs);
                Marshal.ReleaseComObject(wb);
                Marshal.ReleaseComObject(wbs);
                Marshal.ReleaseComObject(excelApp);
                ws = null;
                shs = null;
                wb = null;
                wbs = null;
                excelApp = null;

                GC.Collect();
            }
        }
    }
}
