const { app, BrowserWindow, Menu } = require('electron');
const path = require('path');
function createWindow() {
  const win = new BrowserWindow({
    width: 1440, height: 900, minWidth: 900, minHeight: 600,
    title: 'Песочница миров', backgroundColor: '#191919', autoHideMenuBar: true,
    webPreferences: { contextIsolation: true }
  });
  win.loadFile(path.join(__dirname, 'game', 'index.html'));
  win.maximize();
}
Menu.setApplicationMenu(null);
app.whenReady().then(createWindow);
app.on('window-all-closed', () => app.quit());
