﻿using DoomLauncher.DataSources;
using DoomLauncher.Interfaces;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace DoomLauncher
{
    public partial class TagEdit : UserControl
    {
        private Color? m_color;

        public TagEdit()
        {
            InitializeComponent();

            lblFavorite.Text = string.Concat("Favorite ", TagData.FavoriteChar);

            cmbTab.SelectedIndex = 0;
            cmbColor.SelectedIndex = 1;
            cmbExclude.SelectedIndex = 1;
            cmbFavorite.SelectedIndex = 1;
            m_color = Color.Black;

            Load += TagEdit_Load;
        }

        private void TagEdit_Load(object sender, EventArgs e)
        {
            txtName.Select();
        }

        public void SetDataSource(ITagData tag)
        {
            txtName.Text = tag.Name;
            cmbTab.SelectedIndex = tag.HasTab ? 0 : 1;
            cmbColor.SelectedIndex = tag.HasColor ? 0 : 1;
            cmbExclude.SelectedIndex = tag.ExcludeFromOtherTabs ? 0 : 1;
            cmbFavorite.SelectedIndex = tag.Favorite ? 0 : 1;

            if (tag.HasColor && tag.Color.HasValue)
                m_color = pnlColor.BackColor = Color.FromArgb(tag.Color.Value);
        }

        public void GetDataSource(ITagData tag)
        {
            tag.Name = txtName.Text;
            tag.HasTab = cmbTab.SelectedIndex == 0;
            tag.HasColor = cmbColor.SelectedIndex == 0;
            tag.ExcludeFromOtherTabs = cmbExclude.SelectedIndex == 0;
            tag.Favorite = cmbFavorite.SelectedIndex == 0;

            if (m_color.HasValue)
                tag.Color = m_color.Value.ToArgb();
            else
                tag.Color = null;            
        }

        private void btnSelect_Click(object sender, EventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                pnlColor.BackColor = dialog.Color;
                m_color = dialog.Color;
            }
        }

        private void cmbColor_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnSelect.Visible = pnlColor.Visible = cmbColor.SelectedIndex == 0;
        }
    }
}
