using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Yaml;
using System.Yaml.Serialization;
using System.Runtime.InteropServices;


namespace Lacco
{
    public partial class Lacco : Form
	{	
		#region クラス定義

		// 全体設定
        public class Setting
        {
            public string Html { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
			public Keys[] ShowKeys { get; set; }	// ResidentがTrueの場合のみ有効、表示するためのキー、先頭のみUP時、それ以降は修飾キー(ALT,Ctrl,Shift)のDOWN継続のみ
			public bool Resident { get; set; }		// 常駐アプリにするか
            public Command[] Cmds { get; set; }
			public bool ScrollBar { get; set;  }	// スクロールバーを表示する
			public bool Edit { get; set; }	// 編集モード
			public string FileExt { get; set; }	// 保存時の拡張子
        };
		// command情報
        public class Command
        {
            public string Tag { get; set; }
            public string Id { get; set; }
            public string Name { get; set; }
            public string Text { get; set; }
            public string Value { get; set; }
            public Command[] AddChild { get; set; }
            public string Run { get; set; }
            public string[] Args { get; set; }
			public string Property { get; set; }
			public string PropType { get; set; }
        };

		#endregion クラス定義

		#region プロパティ

		string m_yamlPath = null;
		string m_currentPath = null;
		Setting m_setting = null;
		string m_htmlPath = null;
		bool m_setup = false;	// セットアップ済みかどうか


		// セッティング取得
		public Setting setting
		{
			get { return m_setting; }
		}

		Hashtable m_property = new Hashtable();
		string m_editPath = "out.yaml";

		#endregion プロパティ


		#region 初期化、後処理

		public Lacco(string _yamlPath)
        {
            InitializeComponent();
			// ショートカットを禁止する
			webBrowser1.WebBrowserShortcutsEnabled = false;

			// YAMLセットアップ
			if (!SetupSetting(_yamlPath)) { return; }

			// エディットファイルロード
			LoadEditFile( m_editPath );

			// 常駐アプリで表示キーが指定されていれば
			if (m_setting.Resident && m_setting.ShowKeys != null && m_setting.ShowKeys.Length > 0)
			{
				// グローバルキーフック
				GlobalKeyboard.SetCallback(CallbackKeyboard);
			}
        }

        ~Lacco()
        {
            if (notifyIcon1 != null) { notifyIcon1.Dispose(); }
        }

		// セットアップ
		public bool SetupSetting(string _yamlPath)
		{
			// YAMLシリアライザー
			var yaml = new YamlSerializer();
			object[] yamlObj = yaml.DeserializeFromFile(_yamlPath);
			if (yamlObj == null || yamlObj[0] == null) { return false; }

			Lacco.Setting set = (Lacco.Setting)yamlObj[0];
			if (set != null)
			{
				m_yamlPath = _yamlPath;
				m_setting = set;
				
				// yamlからカレントパス取得
				System.IO.DirectoryInfo dirInfo = System.IO.Directory.GetParent(_yamlPath);
				m_currentPath = dirInfo.FullName;

				// htmlパス取得
				string htmlPath = GetRootPath( set.Html );
				// URL変更
				this.webBrowser1.Url = new Uri(htmlPath);
				// 幅高さ変更
				if (set.Width > 0)
				{
					this.Width = set.Width;
				}
				if (set.Height > 0)
				{
					this.Height = set.Height;
				}
				// スクロールバーの表示非表示
				webBrowser1.ScrollBarsEnabled = m_setting.ScrollBar;
			}

			return set != null;
		}

		// エディットファイルロード
		bool LoadEditFile(string _yamlPath)
		{
			// ファイルがなければ失敗
			if (!System.IO.File.Exists(m_editPath)) { return false; }
			// YAMLシリアライザー
			var yaml = new YamlSerializer();
			object[] yamlObj = yaml.DeserializeFromFile(_yamlPath);
			if (yamlObj == null || yamlObj[0] == null) { return false; }

			m_property = (Hashtable)yamlObj[0];

			return m_property != null;
		}

		// エディットファイルセーブ
		bool SaveEditFile(string _yamlPath)
		{
			if (m_property == null) { return false; }
			var yaml = new YamlSerializer();
			yaml.SerializeToFile(_yamlPath, m_property);

			return true;
		}

		#endregion 初期化、後処理

		#region 汎用関数

		// 絶対パス取得
		string GetRootPath(string _path)
		{
			// 相対パスなら
			if (!System.IO.Path.IsPathRooted(_path))
			{
				return System.IO.Path.Combine(m_currentPath, _path);
			}
			return _path;
		}

		#endregion 

		#region システムコード(unsafe)

		// 環境変数の再読み込み(WinAPI関数)
		[DllImport("shell32.dll")]
		extern unsafe static bool RegenerateUserEnvironment(void** lpEnvironment, bool bUpdate);


		// 環境変数のリロード
		unsafe private static void RegenerateUserEnviroment()
		{
			void* p;
			RegenerateUserEnvironment(&p, true);
		}

		#endregion	システムコード(unsafe)

		#region ウィンドウコールバック

		// キー制御
		private void Lacco_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
		{
			// リロード
			if (webBrowser1.ReadyState == WebBrowserReadyState.Complete && (Keys)e.KeyCode == Keys.F5)
			{
				// 環境変数のリロード
				RegenerateUserEnviroment();
				// 一度ページをなくす
				webBrowser1.Url = new Uri("about:blank");
				m_htmlPath = string.Empty;
				// 設定もリロード
				SetupSetting(m_yamlPath);
			}
		}
		// HTMLロード完了時
        private void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
			// ページのリトライは受け付けない
			if (e.Url.LocalPath == m_htmlPath) { return; }
			m_htmlPath = e.Url.LocalPath;
			// 無しページは無視
			if (e.Url.ToString() == "about:blank") { return;  }

            HtmlDocument html = webBrowser1.Document;

			// 初回セットアップ時のみアイコン設定
			if (m_setup == false)
			{
				// アイコンセットアップ
				SetupIcon(html);
			}

			// セットアップHTML
			SetupHtml(html, m_setting.Cmds);

			m_setup = true;
        }

		// 閉じる
		private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
		{
			// タスクアイコン表示中
			if (notifyIcon1.Visible)
			{
				// 常駐アプリならウィンドウを消すだけ、表示されている場合のみ(Windows終了時の暫定対策)
				if (m_setting.Resident && Visible )
				{
					// 非表示に
					Hide();
					e.Cancel = true;
				}
				// 終了
				else
				{
					notifyIcon1.Visible = false;
					Application.Exit();
					// ファイル保存
					SaveEditFile(m_editPath);
				}
			}
		}
		// 非アクティブになったとき
		private void Lacco_Deactivate(object sender, EventArgs e)
		{
			// 非表示に
			//   Hide();
		}

		#region タスクメニューコールバック

		// バージョン情報
		private void VersionToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Version ver = new Version();
			ver.ShowDialog(this);
		}
		// 終了メニュー
		private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			notifyIcon1.Visible = false;
			Application.Exit();
		}
		// タスクアイコンダブルクリック
		private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			// 表示
			Show();
			// アクティブに
			Activate();
		}

		#endregion タスクメニューコールバック

		// グローバルキーのコールバック
		private bool CallbackKeyboard(int _keyEvent, GlobalKeyboard.KeybordCaptureEventArgs ev)
		{
			if (_keyEvent == GlobalKeyboard.WM_KEYUP)
			{
				Keys[] keys = m_setting.ShowKeys;
				// キーが一致したら
				if (keys[0] == (Keys)ev.KeyCode)
				{
					bool show = true;
					// キーがほかにもある
					if (keys.Length > 1)
					{
						// 修飾キー
						Keys modifierKey = Control.ModifierKeys;
						for (int i = 1; i < keys.Length; ++i)
						{
							Keys key = keys[i];
							if ((modifierKey & key) != key)
							{
								show = false;
								break;
							}
						}
					}
					if (show)
					{
						// アクティブ時
						if (Visible)//&& Form.ActiveForm == this)
						{
							// 非表示に
							Hide();
						}
						else
						{
							// 表示する
							Show();
							// アクティブに
							Activate();
						}
						ev.Cancel = true;
						return true;
					}
				}
			}
			return false;
		}

		#endregion ウィンドウコールバック

		#region HTMLセットアップ

		// アイコン生成
        private Icon CreateIcon(string _path)
        {
            using (WebClient webClient = new WebClient())
            using (MemoryStream stream = new MemoryStream(webClient.DownloadData(_path)))
            {
                return new Icon(stream);
            }
        }

		// アイコンセットアップ
		private bool SetupIcon(HtmlDocument _html)
		{
			HtmlElementCollection links = _html.GetElementsByTagName("link");
			// アイコン取得
			foreach (HtmlElement link in links)
			{
				string rel = link.GetAttribute("rel");
				// Icon操作なら
				if (rel.IndexOf("icon") >= 0)
				{
					// パス取得
					string iconPath = link.GetAttribute("href");
					// ディレクトリ情報取得
					string path = webBrowser1.Url.ToString();
					// 相対パスで取得
					System.IO.DirectoryInfo dirInfo = System.IO.Directory.GetParent(path.Replace("file:///", ""));
					this.Icon = CreateIcon(dirInfo.FullName + "/" + iconPath);
                    notifyIcon1.Icon = this.Icon;
					return true;
				}
			}
			return false;
		}

		// セットアップHTML
		private void SetupHtml(HtmlDocument html, Command[] cmds)
		{
			if (cmds == null) { return; }

			// タイトル変更
			if (m_setup == false)
			{
				this.Text = html.Title;
				if (this.notifyIcon1 != null) { this.notifyIcon1.Text = html.Title; }

				// ウィンドウサイズ反映
				if (m_setting.Width <= 0 || m_setting.Height <= 0)
				{
					Size size = this.ClientSize;
					if (m_setting.Width <= 0)
					{
						size.Width = webBrowser1.Document.Body.ScrollRectangle.Size.Width;
						// スクロールバー分足す
						if (webBrowser1.ScrollBarsEnabled)
						{
							size.Width += 30;
						}
					}
					if (m_setting.Height <= 0)
					{
						size.Height = webBrowser1.Document.Body.ScrollRectangle.Size.Height;
					}
					this.ClientSize = size;
				}
			}

			Hashtable hashTag = new Hashtable();

			// コマンドループ
			foreach (Command cmd in cmds)
			{
				HtmlElementCollection elems = null;
				// ID指定なら
				if (!string.IsNullOrEmpty(cmd.Id))
				{
					HtmlElement elem = html.GetElementById(cmd.Id);
					if (elem == null) { continue; }

					// エレメントセットアップ
					SetupHtmlElement(elem, cmd);
					continue;
				}

				// タグ指定なら
				if (!string.IsNullOrEmpty(cmd.Tag))
				{
					// すでにタグが登録されているか
					if (!hashTag.Contains(cmd.Tag))
					{
						hashTag[cmd.Tag] = elems = html.GetElementsByTagName(cmd.Tag);
					}
					// 登録済みならハッシュから取得
					else
					{
						elems = (HtmlElementCollection)hashTag[cmd.Tag];
					}
					// タグなし
					if (elems == null) { continue; }
				}
				// タグ指定もなければ
				else
				{
					if (!hashTag.Contains("All"))
					{
						hashTag["All"] = elems = html.All;
					}
					// 登録済みならハッシュから取得
					else
					{
						elems = (HtmlElementCollection)hashTag["All"];
					}
				}
				// まだエレメントが決まってなければ
				if (elems != null)
				{
					if (string.IsNullOrEmpty(cmd.Name)) { continue; }  // 名前指定がなければ探しようがない
					// 名前が一致するものを取得
					var result = from HtmlElement el in elems where el.Name == cmd.Name select el;
					foreach (var elem in result)
					{
						// 全部に登録してみる
						SetupHtmlElement(elem, cmd);
					}
				}
			}
		}

        // エレメントセットアップ
        private void SetupHtmlElement(HtmlElement _elem, Command _cmd)
        {
            if (!string.IsNullOrEmpty(_cmd.Text))
            {
				_elem.InnerText = ConvText(_cmd.Text);
            }
            // ID上書き
            if (!string.IsNullOrEmpty(_cmd.Id) && _elem.Id != _cmd.Id)
            {
                _elem.Id = _cmd.Id;
            }
            // Name上書き
            if (!string.IsNullOrEmpty(_cmd.Name) && _elem.Name != _cmd.Name)
            {
                _elem.Name = _cmd.Name;
            }
            // 実行指定
            if (!string.IsNullOrEmpty(_cmd.Run))
            {
                // イベントハンドラー指定
                _elem.AttachEventHandler( "onclick", (_sender, _e) =>
                    {
                        RunElementEvent( _elem, _cmd );
                    }
                );
            }

			// 子供追加設定
			if (_cmd.AddChild != null)
			{
				// 子供ループ
				foreach (var childCmd in _cmd.AddChild)
				{
					if (string.IsNullOrEmpty(childCmd.Tag)) { continue; }

					HtmlElement add = webBrowser1.Document.CreateElement(childCmd.Tag);
					if (add == null) { continue; }
					// エレメントセットアップ
					SetupHtmlElement(add, childCmd);
					// 子供に追加
					_elem.AppendChild(add);
				}
			}

			if (!string.IsNullOrEmpty(_cmd.Value))
			{
				_elem.SetAttribute("value", _cmd.Value);
			}
			
			// プロパティ指定
			if (!string.IsNullOrEmpty(_cmd.Property))
			{
				// 保存された値の取得
				{
					Hashtable table = null;
					string prop = null;
					if (GetProperty(ref table, ref prop, _cmd.Property, _elem, true) && table.Contains(prop))
					{
						// 値を設定
						object val = table[prop];
						if (val.GetType() != typeof(Hashtable))
						{
							_elem.SetAttribute("value", val.ToString());
						}
					}
				}
				// 変更があったとき
				_elem.AttachEventHandler("onchange", (_sender, _e) =>
				{
					Hashtable table = null;
					string prop = null;
					if (GetProperty(ref table, ref prop, _cmd.Property, _elem))
					{
						// プロパティタイプがあれば
						if (!string.IsNullOrEmpty(_cmd.PropType))
						{
							Type t = Type.GetType(_cmd.PropType);
							if (t == typeof(Hashtable) )//_cmd.PropType == "Hashtable")
							{
								// ハッシュテーブルじゃない場合
								if (table[prop] == null || table[prop].GetType() != typeof(Hashtable))
								{
									table[prop] = new Hashtable();
								}
								Hashtable hash = (Hashtable)table[prop];
								string val = _elem.GetAttribute("value");
								// 要素がまだなければNULLで登録しておく
								if (!hash.Contains(val))
								{
									hash[_elem.GetAttribute("value")] = null;
								}
							}
						}
						else
						{
							// 値を設定
							table[prop] = _elem.GetAttribute("value");
						}
					}
				}
				);
			}
        }

		// プロパティ取得
		bool GetProperty(ref Hashtable _retTable, ref string _retPropName, string _propPath, HtmlElement _currentElem, bool _setup = false )
		{			
			// 文字を.区切りに
			string[] props = _propPath.Split('.');
			Hashtable current = m_property;
			for (int i = 0; i < props.Length; ++i)
			{
				string prop = props[i];
				// タグ指定
				if (prop[0] == '<')
				{
					// エレメント取得
					HtmlElement[] elems = GetElementByText(prop, _currentElem);
					if (elems != null)
					{
						// プロパティ名を値から取得
						prop = elems[0].GetAttribute("value");
						// ついでにカレントのエレメントのセットアップを行う
						if (_currentElem != null && _setup)
						{
							// 親のタグに変更があった場合
							elems[0].AttachEventHandler("onchange", (_sender, _e) =>
								{
									// 値を更新する
									UpdateProperty(_currentElem, _propPath);
								}
							);
						}
					}
				}
				// まだ子供がいる前提でキーがあれば
				if (i + 1 < props.Length && current.Contains(prop))
				{
					object p = current[prop];
					if (p == null) { current[prop] = p = new Hashtable(); }
					current = (Hashtable)p;
					if (current == null) { break; }
					continue;
				}
				// 途中がない
				else if (i + 1 < props.Length)
				{
					break;
				}
				// 戻り値設定
				_retTable = current;
				_retPropName = prop;
				return true;
			}
			return false;
		}

		// プロパティ更新
		bool UpdateProperty(HtmlElement _elem, string _propPath)
		{
			Hashtable table = null;
			string prop = null;
			if (GetProperty(ref table, ref prop, _propPath, _elem) && table.Contains(prop))
			{
				// 値を設定
				object val = table[prop];
				if (val != null)
				{
					// ハッシュテーブルでなければ
					if (val.GetType() != typeof(Hashtable))
					{
						_elem.SetAttribute("value", val.ToString());
					}
					// ハッシュテーブルなら先頭キーでも選択させておく
					else
					{
						 table = (Hashtable)val;
						// 先頭キーを設定
						foreach( string str in table.Keys )
						{
							_elem.SetAttribute("value", str);
							break;
						}
					}
				}
				else
				{
					_elem.SetAttribute("value", "");
				}
				return true;
			}
			_elem.SetAttribute("value", "");
			return false;
		}


		#endregion HTMLセットアップ

		#region HTMLイベント

		// 実行イベント
        void RunElementEvent(HtmlElement _elem, Command _cmd)
        {
            string args = string.Empty;
            foreach (var a in _cmd.Args)
            {
                string add = a;
                // タグ指定だったら
                if (a[0] == '<')
                {
                    add = string.Empty;

					HtmlElement[] result = GetElementByText(a, _elem);
					foreach (HtmlElement e in result)
					{
						if (!string.IsNullOrEmpty(add))	// 次の引数からは区切り文字追加
						{
							add += ',';
						}
						// 選択リストで複数選択可能なら
						if (e.TagName == "SELECT" && e.GetAttribute("multiple") == "True")
						{
							// 選択したリスト全てを含める
							var opts = from HtmlElement opt in e.All where opt.GetAttribute("selected") == "True" select opt;
							if (opts != null)
							{
								foreach (var opt in opts)
								{
									if (!string.IsNullOrEmpty(add))	// 次の引数からは区切り文字追加
									{
										add += ',';
									}
									add += opt.GetAttribute("value");
								}
							}
						}
						else
						{
							add += e.GetAttribute("value");
						}
					}
                    // ダブルクォーテーションで囲む
                    add = "\"" + add + "\"";
                }
                // 空白を入れる
                if (!string.IsNullOrEmpty(args)) { args += " "; }
                args += add;
            }
            // プロセス実行
			string run = ConvText(_cmd.Run);
			run = GetRootPath(run);
			args = ConvText(args);
            System.Diagnostics.Process p = System.Diagnostics.Process.Start(run, args);
        }

		// テキスト変換
		private string ConvText(string _text)
		{
			if (_text.IndexOf('%') >= 0)
			{
				// 環境変数展開
				return System.Environment.ExpandEnvironmentVariables(_text);
			}
			return _text;
		}

		// タグまたはID指定からエレメント取得
		private HtmlElement[] GetElementByText(string _tag, HtmlElement _currentElem)
		{
			// 自分のタグ
			if (_tag == "<$>")
			{
				return new HtmlElement[] { _currentElem };
			}
			// <>のタグの囲いをカット
			string str = _tag.Substring(1, _tag.Length - 2);
			// セパレータで切り分け
			string[] param = str.Split(':');
			// セパレートあり
			if (param.Length > 1)
			{
				// 前方がタグ、後方が名前
				string tag = param[0];
				string name = param[1];
				// タグで検索
				var result = from HtmlElement e in webBrowser1.Document.GetElementsByTagName(tag) where e.Name == name select e;
				if (result != null)
				{
					// 複数存在する
					if (result.Count() > 0)
					{
						// インプット指定なら
						if ( tag.ToLower() == "input" )
						{
							string type = result.First().GetAttribute("type");
							// チェックボックスなら
							var result2 = from HtmlElement e in result where e.GetAttribute("type") == type select e;
							// 同じタイプが複数存在の場合、選択されているものだけ返す
							if (result2 != null && result2.Count() > 0)
							{
								result = from HtmlElement e in result2 where e.GetAttribute("checked") == "True" select e; ;
							}
						}
					}

					HtmlElement[] ret = new HtmlElement[result.Count()];
					int idx = 0;
					foreach (HtmlElement e in result)
					{
						ret[idx++] = e;
					}
					return ret;
				}
			}
			else
			{
				// ID検索
				string id = param[0];
				HtmlElement e = webBrowser1.Document.GetElementById(id);
				if (e != null)
				{
					return new HtmlElement[] { e };
				}
			}
			return null;
		}

		#endregion HTMLイベント

	}
}
