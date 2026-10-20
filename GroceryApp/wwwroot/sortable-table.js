window.sortableTable = {
    init: function(tableId) {
        const table = document.getElementById(tableId);
        if (!table) return;

        const headers = table.querySelectorAll('th.sortable');
        
        headers.forEach((header, index) => {
            header.addEventListener('click', () => {
                this.sortTable(table, index, header);
            });
        });
    },

    sortTable: function(table, columnIndex, header) {
        const tbody = table.querySelector('tbody');
        const rows = Array.from(tbody.querySelectorAll('tr'));
        
        // Determine sort direction
        const currentSort = header.classList.contains('sort-asc') ? 'asc' : 
                          header.classList.contains('sort-desc') ? 'desc' : 'none';
        
        const newSort = currentSort === 'asc' ? 'desc' : 'asc';
        
        // Clear all sort classes from headers
        table.querySelectorAll('th').forEach(th => {
            th.classList.remove('sort-asc', 'sort-desc');
        });
        
        // Add new sort class
        header.classList.add(newSort === 'asc' ? 'sort-asc' : 'sort-desc');
        
        // Sort rows
        rows.sort((a, b) => {
            const aCell = a.cells[columnIndex];
            const bCell = b.cells[columnIndex];
            
            let aValue = this.getCellValue(aCell);
            let bValue = this.getCellValue(bCell);
            
            // Handle numeric values
            if (!isNaN(aValue) && !isNaN(bValue)) {
                aValue = parseFloat(aValue);
                bValue = parseFloat(bValue);
            }
            
            if (aValue < bValue) return newSort === 'asc' ? -1 : 1;
            if (aValue > bValue) return newSort === 'asc' ? 1 : -1;
            return 0;
        });
        
        // Reorder rows in DOM
        rows.forEach(row => tbody.appendChild(row));
    },

    getCellValue: function(cell) {
        // Extract text content, removing currency symbols and percentage signs
        let text = cell.textContent.trim();
        text = text.replace(/[$,%]/g, ''); // Remove $, %, and comma
        return text;
    }
};