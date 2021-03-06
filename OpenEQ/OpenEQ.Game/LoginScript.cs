using System;
using SiliconStudio.Xenko.UI.Controls;
using OpenEQ.Network;
using SiliconStudio.Xenko.UI.Panels;
using SiliconStudio.Xenko.UI;
using SiliconStudio.Xenko.UI.Events;
using SiliconStudio.Core.Mathematics;
using SiliconStudio.Xenko.Engine;
using System.Linq;
using OpenEQ.Configuration;

namespace OpenEQ
{
    public class LoginScript : UIScript
    {
        public string LoginServer = "login.eqemulator.net:5998";
        LoginStream login;

#pragma warning disable 0649
        [PageElement] Button loginButton;
        [PageElement] EditText username, password;
        [PageElement] TextBlock status;
        [PageElement] UniformGrid authSection;
        [PageElement] StackPanel serverListSection;
        [PageElement] Grid serverGrid;
        [PageElement] TextBlock serverNameHeader;
#pragma warning restore 0649

        public override void Setup() {
            authSection.Visibility = Visibility.Visible;
            serverListSection.Visibility = Visibility.Hidden;

            login = new LoginStream(OpenEQConfiguration.Instance.LoginServerAddress, OpenEQConfiguration.Instance.LoginServerPort);

            loginButton.Click += (sender, e) => {
                loginButton.IsEnabled = false;
                login.Login(username.Text, password.Text);
                status.Text = "Sending login...";
            };

            login.LoginSuccess += (sender, success) => {
                if(success) {
                    status.Text = "Login successful. Retrieving server list.";
                    login.RequestServerList();
                } else {
                    status.Text = "Login failed.";
                    loginButton.IsEnabled = true;
                }
            };

            login.ServerList += (sender, list) => {
                authSection.Visibility = Visibility.Hidden;
                serverListSection.Visibility = Visibility.Visible;

                // Sort this list first.
                list = list.OrderByDescending(a => a.ServerListID).ThenBy(b => b.Status).ThenByDescending(c => c.PlayersOnline).ThenBy(d => d.Longname).ToList();

                var i = 0;
                foreach(var server in list) {
                    serverGrid.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 25));
                    var namefield = new TextBlock { Text = server.Longname, Font = serverNameHeader.Font, TextSize = serverNameHeader.TextSize, TextColor = serverNameHeader.TextColor };
                    namefield.SetGridColumn(0);
                    namefield.SetGridRow(i);
                    serverGrid.Children.Add(namefield);
                    var statusfield = new TextBlock { Text = server.Status == ServerStatus.Up ? server.PlayersOnline.ToString() : "Down", Font = serverNameHeader.Font, TextSize = serverNameHeader.TextSize, TextColor = serverNameHeader.TextColor };
                    statusfield.SetGridColumn(1);
                    statusfield.SetGridRow(i);
                    serverGrid.Children.Add(statusfield);
                    var serverButton = new Button { MouseOverImage = loginButton.MouseOverImage, NotPressedImage = loginButton.NotPressedImage, PressedImage = loginButton.PressedImage };
                    var buttonLabel = new TextBlock { Text = "Play", Font = serverNameHeader.Font, TextSize = 8, TextColor = serverNameHeader.TextColor, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                    serverButton.Content = buttonLabel;
                    serverButton.SetGridColumn(2);
                    serverButton.SetGridRow(i++);
                    serverGrid.Children.Add(serverButton);
                    serverButton.Click += (s, e) => {
                        ((Button)s).IsEnabled = false;
                        Console.WriteLine($"Sending play request for {server.Longname}");
                        login.Play(server);
                    };
                }
            };

            login.PlaySuccess += (sender, server) => {
                Console.WriteLine($"Play response: { server }");
                if(server == null)
                    return;

                SceneSystem.SceneInstance.Scene = Content.Load<Scene>("WorldScene");
                var wui = SceneSystem.SceneInstance.Scene.Entities.Where(x => x.Name == "WorldDialog").First();
                foreach(var comp in wui.Components)
                    if(comp is WorldScript)
                        ((WorldScript) comp).world = new WorldStream(server.Value.WorldIP, 9000, login.accountID, login.sessionKey);
            };
        }

        public override void Update() {
        }
    }
}
