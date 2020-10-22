﻿using DoomLauncher.DataSources;
using DoomLauncher.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace DoomLauncher
{
    public partial class TagControl : UserControl
    {
        private IDataSourceAdapter m_adapter;
        private readonly List<ITagData> m_addTags = new List<ITagData>();
        private readonly List<ITagData> m_editTags = new List<ITagData>();
        private readonly List<ITagData> m_deleteTags = new List<ITagData>();

        public TagControl()
        {
            InitializeComponent();

            GameFileViewControl.StyleGrid(dgvTags);
            dgvTags.AllowUserToResizeRows = false;
            dgvTags.MultiSelect = false;

            DataGridViewColumn col = new DataGridViewTextBoxColumn();
            col.HeaderText = "Name";
            col.Name = "Name";
            col.DataPropertyName = "Name";
            dgvTags.Columns.Add(col);

            col = new DataGridViewTextBoxColumn();
            col.HeaderText = "Display Tab";
            col.Name = "HasTab";
            col.DataPropertyName = "HasTab";
            dgvTags.Columns.Add(col);

            col = new DataGridViewTextBoxColumn();
            col.HeaderText = "Exclude";
            col.Name = "ExcludeFromOtherTabs";
            col.DataPropertyName = "ExcludeFromOtherTabs";
            dgvTags.Columns.Add(col);

            col = new DataGridViewTextBoxColumn();
            col.HeaderText = "Favorite";
            col.Name = "Favorite";
            col.DataPropertyName = "Favorite";
            dgvTags.Columns.Add(col);

            col = new DataGridViewTextBoxColumn();
            col.HeaderText = "Display Color";
            col.Name = "HasColor";
            col.DataPropertyName = "HasColor";
            dgvTags.Columns.Add(col);

            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        public void Init(IDataSourceAdapter adapter)
        {
            m_adapter = adapter;
            dgvTags.DataSource = adapter.GetTags().OrderBy(x => x.Name).ToList();
        }

        public ITagData[] AddedTags
        {
            get { return m_addTags.ToArray();  }
        }

        public ITagData[] EditedTags
        {
            get { return m_editTags.ToArray(); }
        }

        public ITagData[] DeletedTags
        {
            get { return m_deleteTags.ToArray(); }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            TagEditForm form = new TagEditForm();
            form.StartPosition = FormStartPosition.CenterParent;

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                TagData tag = new TagData();
                form.TagEditControl.GetDataSource(tag);

                if (!IsTagNameUnique(tag))
                {
                    MessageBox.Show(this, "Tag name must be unique and not empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    m_adapter.InsertTag(tag);
                    Init(m_adapter);

                    IEnumerable<ITagData> check = m_adapter.GetTags().Where(x => x.Name == tag.Name);

                    if (check.Any())
                    {
                        if (m_addTags.Contains(check.First()))
                            m_addTags.Remove(check.First());

                        m_addTags.Add(check.First());
                    }
                }

                if (dgvTags.Rows.Count > 0)
                    dgvTags.Rows[dgvTags.Rows.Count - 1].Selected = true;
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (dgvTags.SelectedRows.Count > 0 && dgvTags.SelectedRows[0].DataBoundItem is ITagData tag)
            {
                TagEditForm form = new TagEditForm();
                form.TagEditControl.SetDataSource(tag);
                form.StartPosition = FormStartPosition.CenterParent;

                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    form.TagEditControl.GetDataSource(tag);

                    if (!IsTagNameUnique(tag))
                    {
                        MessageBox.Show(this, "Tag name must be unique and not empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Init(m_adapter);
                    }
                    else
                    {
                        m_adapter.UpdateTag(tag);
                        Init(m_adapter);

                        if (m_editTags.Contains(tag))
                            m_editTags.Remove(tag);

                        m_editTags.Add(tag);
                    }

                    foreach (DataGridViewRow row in dgvTags.Rows)
                    {
                        if (row.DataBoundItem.Equals(tag))
                        {
                            dgvTags.FirstDisplayedScrollingRowIndex = row.Index;
                            row.Selected = true;
                            break;
                        }
                    }
                }
            }
        }

        private bool IsTagNameUnique(ITagData tag)
        {
            IEnumerable<ITagData> check = m_adapter.GetTags().Where(x => x.Name.Equals(tag.Name, StringComparison.CurrentCultureIgnoreCase) && !x.Equals(tag));
            return !(string.IsNullOrEmpty(tag.Name) || check.Any() || TabKeys.KeyNames.ToList().FindAll(x=> tag.Name.Equals(x, StringComparison.CurrentCultureIgnoreCase)).Any());
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (dgvTags.SelectedRows.Count > 0 && MessageBox.Show(this, "Are you sure you want to delete this tag?",
                "Delete", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK && 
                dgvTags.SelectedRows[0].DataBoundItem is ITagData tag)
            {
                m_adapter.DeleteTag(tag);
                m_adapter.DeleteTagMapping(tag.TagID);

                Init(m_adapter);

                if (m_deleteTags.Contains(tag))
                    m_deleteTags.Remove(tag);

                m_deleteTags.Add(tag);
            }
        }
    }
}
