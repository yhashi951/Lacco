using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Yaml;
using System.Yaml.Serialization;
using System.Collections;

namespace Lacco
{
    static class Program
    {
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);


			// Yamlファイルパス
			string yamlPath = "main.yaml";
			string[] cmdlineArgs = Environment.GetCommandLineArgs();
			if (cmdlineArgs != null && cmdlineArgs.Length > 1)
			{
				yamlPath = cmdlineArgs[1];	// ファイルパス指定
			}

#if false
			Hashtable data = new Hashtable();
			data["TestText"] = "テキスト1";
			data["TestValue"] = 100;
			Hashtable data2 = new Hashtable();
			data["_Data"] = data2;
			data2["TestValue"] = 200;

			string yamlPath2 = "out.yaml";
			yaml.SerializeToFile(yamlPath2, data);
#endif

			Lacco browser = new Lacco(yamlPath);
			if (browser.setting == null)
			{
				MessageBox.Show("初期化に失敗しました。設定を確認してください。\n" + yamlPath, "初期化エラー");
				Application.Exit();
			}
            IntPtr dummy = browser.Handle; //ハンドルを確保
			// 常駐でない場合は引数にFormを渡す
			if (browser.setting.Resident == false)
			{
				Application.Run(browser);
			}
			else
			{
				Application.Run();
			}
        }
    }
}
