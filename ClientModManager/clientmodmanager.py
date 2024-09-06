#! /usr/bin/env python3

import sys
import os
import io
import json
import requests
import zipfile
import wx

def is_within_directory(directory, target):
    """
    Check if the target path is within the given directory.
    This prevents directory traversal attacks.
    """
    abs_directory = os.path.abspath(directory)
    abs_target = os.path.abspath(target)
    return os.path.commonpath([abs_directory]) == os.path.commonpath([abs_directory, abs_target])


class Manager(wx.Frame):
    def __init__(self, *args, **kw):
        # ensure the parent's __init__ is called
        super(Manager, self).__init__(*args, **kw)
                # Set up the panel
        panel = wx.Panel(self)

        # Create a horizontal box sizer for the input field and button
        hbox = wx.BoxSizer(wx.HORIZONTAL)

        # Create the text input field
        self.input_text = wx.TextCtrl(panel)
        self.input_text.SetHint('server queueing URL')

        hbox.Add(wx.StaticText(panel, label='Server URL:'), flag=wx.ALIGN_CENTER_VERTICAL)
        hbox.Add(self.input_text, proportion=1, flag=wx.EXPAND|wx.ALL, border=5)

        # Create the button
        self.button = wx.Button(panel, label='Sync mods')
        hbox.Add(self.button, flag=wx.EXPAND|wx.ALL, border=5)

        # Bind the button event to a handler
        self.button.Bind(wx.EVT_BUTTON, self.on_button_click)

        # Create a vertical box sizer to arrange the input field, button, and text area vertically
        vbox = wx.BoxSizer(wx.VERTICAL)
        vbox.Add(hbox, flag=wx.EXPAND|wx.ALL, border=5)

        # Create the text area for logging
        self.log_text = wx.TextCtrl(panel, style=wx.TE_MULTILINE | wx.TE_READONLY | wx.TE_RICH2)
        vbox.Add(self.log_text, proportion=1, flag=wx.EXPAND|wx.ALL, border=5)

        # Set the sizer for the panel
        panel.SetSizer(vbox)

        # Set the frame properties
        self.SetSize((400, 300))
        self.SetTitle('MyDu client mod synchronization')
        self.Centre()
    def log(self, txt):
        self.log_text.SetDefaultStyle(wx.TextAttr(wx.Colour(0,0,0)))
        #self.log_text.SetForegroundColour((0,0,0))
        self.log_text.AppendText(txt + '\n')
    def log_error(self, txt):
        self.log_text.SetDefaultStyle(wx.TextAttr(wx.Colour(255,0,0)))
        #self.log_text.SetForegroundColour((255,0,0))
        self.log_text.AppendText('[ERROR] ' + txt + '\n')
    def log_success(self, txt):
        self.log_text.SetDefaultStyle(wx.TextAttr(wx.Colour(0,255,0)))
        #self.log_text.SetForegroundColour((0,255,0))
        self.log_text.AppendText(txt + '\n')
    def on_button_click(self, event):
        """Handler for the button click event."""
        # Get the input from the text field
        input_value = self.input_text.GetValue()

        self.apply_mods(input_value)
    def apply_mods(self, url):
        if not os.path.exists('Game'):
            self.log_error('Manager must be placed above the "Game" directory')
            return
        try:
            cache_dir = 'mods-cache'
            mods_dir = os.path.join('Game', 'data', 'resources_generated', 'mods')
            os.makedirs(cache_dir, exist_ok=True)
            os.makedirs(mods_dir, exist_ok=True)
            cur_cache = os.listdir(cache_dir)
            cur_mods = os.listdir(mods_dir)
            if url == '':
                self.log('Disabling all mods...')
                for f in cur_mods:
                    os.rename(os.path.join(mods_dir, f), os.path.join(cache_dir, f))
                self.log('...done')
                return
            if ':' not in url:
                url = url + ':9630'
            if url[:4] != 'http':
                url = 'http://' + url
            # fetch manifest
            resp = requests.get(url + '/clientmods/manifest.txt')
            if resp.status_code != 200:
                self.log_error('Bad status {} for mod manifest'.format(resp.status_code))
                return
            mods_list = resp.text.split('\n')
            want = list()
            change = False
            for tm in mods_list:
                tm = tm.strip(' \r')
                if len(tm) == 0 or tm[0] == '#':
                    continue
                if tm[-4:] == '.zip':
                    tm = tm[:-4]
                if '/' in tm or '\\' in tm or '?' in tm or '*' in tm:
                    raise Exception('Forbidden character in mod manifest entry')
                want.append(tm)
                if tm in cur_mods:
                    continue
                if tm in cur_cache:
                    self.log('Enabling ' + tm)
                    change = True
                    os.rename(os.path.join(cache_dir, tm), os.path.join(mods_dir, tm))
                    continue
                # we don't have it, download
                change = True
                self.log('downloading ' + tm + '...')
                resp = requests.get(url + '/clientmods/' + tm + '.zip')
                if resp.status_code != 200:
                    self.log_error('Bad status for mod ' + tm)
                    return
                extract_to = os.path.join(mods_dir, tm)
                os.makedirs(extract_to, exist_ok = True)
                with zipfile.ZipFile(io.BytesIO(resp.content)) as zip_file:
                    for member in zip_file.namelist():
                        ext = member.split('.')[-1].lower()
                        if ext == 'exe' or ext == 'dll':
                            raise Exception('forbidden executable in zip file')
                        member_path = os.path.join(extract_to, member)
                        if not is_within_directory(extract_to, member_path):
                            raise Exception(f"Attempted Path Traversal in ZIP File: {member}")
                        zip_file.extract(member, extract_to)
                self.log('...downloaded and extracted' + tm)
            # now disable those we don't want
            for cand in cur_mods:
                if cand not in want:
                    self.log('Disabling ' + cand)
                    change = True
                    os.rename(os.path.join(cache_dir, cand), os.path.join(mods_dir, cand))
            if not change:
                self.log_success('No change, you are good to go')
            else:
                self.log_success('Mods are ready, restart your client to take effect')
        except Exception as e:
            self.log_error('Exception encountered')
            self.log_error('{}'.format(e))
class MyApp(wx.App):
    def OnInit(self):
        frame = Manager(None)
        frame.Show()
        return True

if __name__ == '__main__':
    app = MyApp(False)
    app.MainLoop()