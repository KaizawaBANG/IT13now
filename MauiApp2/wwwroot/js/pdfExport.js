// PDF Export functionality using jsPDF
window.pdfExport = {
    exportTableToPdf: function (tableId, title, filename) {
        // Check if jsPDF is already loaded (UMD build exposes it as window.jspdf)
        let jsPDF;
        if (typeof window.jspdf !== 'undefined' && window.jspdf.jsPDF) {
            jsPDF = window.jspdf.jsPDF;
        } else if (typeof window.jspdf !== 'undefined') {
            // Try direct access
            jsPDF = window.jspdf;
        } else {
            console.error('jsPDF library not loaded. Please ensure it is included in index.html');
            alert('PDF export failed: jsPDF library not loaded. Please refresh the page and try again.');
            return;
        }

        try {
            const doc = new jsPDF('l', 'mm', 'a4'); // Landscape for tables
            
            const table = document.getElementById(tableId);
            if (!table) {
                console.error('Table not found:', tableId);
                alert('Table not found. Please ensure the report table is visible.');
                return;
            }

            // Add title
            doc.setFontSize(16);
            doc.text(title, 145, 15, { align: 'center' });
            
            // Add date
            const now = new Date();
            doc.setFontSize(10);
            doc.text(`Generated: ${now.toLocaleString()}`, 145, 22, { align: 'center' });
            
            // Get table rows
            const rows = [];
            const headerRow = table.querySelector('thead tr');
            if (headerRow) {
                const headers = Array.from(headerRow.querySelectorAll('th')).map(th => th.textContent.trim());
                rows.push(headers);
            }
            
            const bodyRows = table.querySelectorAll('tbody tr');
            bodyRows.forEach(row => {
                const cells = Array.from(row.querySelectorAll('td')).map(td => {
                    // Get text content, handling nested elements
                    const text = td.textContent.trim();
                    return text;
                });
                rows.push(cells);
            });

            // Add footer rows if exists
            const footerRow = table.querySelector('tfoot tr');
            if (footerRow) {
                const footerCells = Array.from(footerRow.querySelectorAll('td')).map(td => td.textContent.trim());
                rows.push(footerCells);
            }
            
            if (rows.length === 0) {
                alert('No data to export.');
                return;
            }
            
            // Calculate column widths dynamically based on content
            const colCount = rows.length > 0 ? rows[0].length : 0;
            if (colCount === 0) {
                alert('No columns found in table.');
                return;
            }
            
            const availableWidth = 270; // A4 landscape width minus margins
            const colWidth = availableWidth / colCount;
            const fontSize = 9;
            
            let y = 35;
            const pageHeight = 190;
            
            rows.forEach((row, index) => {
                // Check if we need a new page
                if (y > pageHeight && index > 0) {
                    doc.addPage();
                    y = 20;
                }

                let x = 15;
                row.forEach((cell, cellIndex) => {
                    doc.setFontSize(index === 0 || index === rows.length - 1 ? fontSize + 1 : fontSize);
                    doc.setFont('helvetica', (index === 0 || index === rows.length - 1) ? 'bold' : 'normal');
                    
                    // Word wrap for long text
                    const maxWidth = colWidth - 2;
                    const lines = doc.splitTextToSize(cell || '', maxWidth);
                    const lineHeight = 5;
                    lines.forEach((line, lineIndex) => {
                        doc.text(line, x, y + (lineIndex * lineHeight), { maxWidth: maxWidth });
                    });
                    x += colWidth;
                });
                
                // Increase y position based on content height
                const maxLines = Math.max(...row.map(cell => {
                    const maxWidth = colWidth - 2;
                    const lines = doc.splitTextToSize(cell || '', maxWidth);
                    return lines.length;
                }));
                const cellHeight = (maxLines * 5) + 2;
                y += cellHeight;
            });
            
            doc.save(filename || 'report.pdf');
        } catch (error) {
            console.error('Error generating PDF:', error);
            alert('Error generating PDF: ' + error.message);
        }
    },

    exportSalesSummaryToPdf: function (tableId, reportNumber, issueDate, startDate, endDate, groupBy, totalTransactions, totalRevenue, totalTax, totalSubtotal, averageTransaction, filename) {
        // Check if jsPDF is already loaded
        let jsPDF;
        if (typeof window.jspdf !== 'undefined' && window.jspdf.jsPDF) {
            jsPDF = window.jspdf.jsPDF;
        } else if (typeof window.jspdf !== 'undefined') {
            jsPDF = window.jspdf;
        } else {
            console.error('jsPDF library not loaded.');
            alert('PDF export failed: jsPDF library not loaded. Please refresh the page and try again.');
            return;
        }

        try {
            const doc = new jsPDF('p', 'mm', 'a4'); // Portrait for report
            
            const table = document.getElementById(tableId);
            if (!table) {
                console.error('Table not found:', tableId);
                alert('Table not found. Please ensure the report table is visible.');
                return;
            }

            // Page dimensions with professional print margins
            const pageWidth = 210;
            const pageHeight = 297;
            const margin = 12;
            const topMargin = 12;
            const contentWidth = pageWidth - (margin * 2);
            let y = topMargin;

            // Helper function to format numbers with currency
            const formatCurrency = (num) => {
                const value = parseFloat(num) || 0;
                return '₱' + value.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
            };

            const formatNumber = (num) => {
                const value = parseFloat(num) || 0;
                return value.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
            };

            // === HEADER SECTION ===
            // Header background with gradient effect (dark blue-gray)
            const headerHeight = 42;
            doc.setFillColor(35, 47, 62); // Darker shade for professional look
            doc.rect(0, 0, pageWidth, headerHeight, 'F');
            
            // Subtle accent line at bottom of header
            doc.setDrawColor(60, 80, 100);
            doc.setLineWidth(0.3);
            doc.line(0, headerHeight, pageWidth, headerHeight);
            
            // Company name/Logo area (left side of header)
            doc.setFontSize(20);
            doc.setFont('helvetica', 'bold');
            doc.setTextColor(255, 255, 255);
            doc.text('QUADTECH', margin, 22);
            
            // Report title (centered in header with better spacing)
            doc.setFontSize(18);
            doc.setFont('helvetica', 'bold');
            doc.setTextColor(255, 255, 255);
            doc.text('SALES SUMMARY REPORT', pageWidth / 2, 28, { align: 'center' });
            
            y = headerHeight + 12; // Start content below header with proper spacing

            // === REPORT METADATA SECTION ===
            // Metadata box with subtle background
            const metadataBoxY = y;
            doc.setFillColor(250, 251, 252);
            doc.rect(margin, metadataBoxY, contentWidth, 28, 'F');
            
            // Border around metadata box
            doc.setDrawColor(230, 235, 240);
            doc.setLineWidth(0.5);
            doc.rect(margin, metadataBoxY, contentWidth, 28, 'S');
            
            y += 6;
            
            doc.setFontSize(8.5);
            doc.setFont('helvetica', 'normal');
            doc.setTextColor(80, 90, 100);
            
            // Report details in two columns with labels
            const metadataLeftX = margin + 6;
            const metadataRightX = pageWidth / 2 + 15;
            let metadataY = y;
            
            // Left column
            doc.setFont('helvetica', 'bold');
            doc.setTextColor(100, 110, 120);
            doc.text('Report Number:', metadataLeftX, metadataY);
            doc.setFont('helvetica', 'normal');
            doc.setTextColor(50, 60, 70);
            doc.text(reportNumber, metadataLeftX + 32, metadataY);
            
            doc.setFont('helvetica', 'bold');
            doc.setTextColor(100, 110, 120);
            doc.text('Period:', metadataLeftX, metadataY + 5);
            doc.setFont('helvetica', 'normal');
            doc.setTextColor(50, 60, 70);
            doc.text(`${startDate} to ${endDate}`, metadataLeftX + 15, metadataY + 5);
            
            // Right column
            doc.setFont('helvetica', 'bold');
            doc.setTextColor(100, 110, 120);
            doc.text('Generated:', metadataRightX, metadataY);
            doc.setFont('helvetica', 'normal');
            doc.setTextColor(50, 60, 70);
            doc.text(issueDate, metadataRightX + 22, metadataY);
            
            doc.setFont('helvetica', 'bold');
            doc.setTextColor(100, 110, 120);
            doc.text('Grouped By:', metadataRightX, metadataY + 5);
            doc.setFont('helvetica', 'normal');
            doc.setTextColor(50, 60, 70);
            doc.text(groupBy, metadataRightX + 26, metadataY + 5);
            
            y = metadataBoxY + 28 + 8; // Move below metadata box

            // Subtle divider line
            doc.setDrawColor(235, 240, 245);
            doc.setLineWidth(0.5);
            doc.line(margin, y, pageWidth - margin, y);
            y += 6;

            // === EXECUTIVE SUMMARY SECTION ===
            doc.setFontSize(13);
            doc.setFont('helvetica', 'bold');
            doc.setTextColor(35, 47, 62);
            doc.text('Executive Summary', margin, y);
            y += 10;

            // Summary boxes with professional design (2x2 layout)
            const boxWidth = (contentWidth - 6) / 2; // Two boxes per row with 6mm spacing
            const boxHeight = 26;
            const boxSpacing = 6;
            const boxVerticalSpacing = 8;
            const boxesStartX = margin;
            let boxX = boxesStartX;
            let boxY = y;

            // Summary metrics from parameters with color accents
            const metrics = [
                { 
                    label: 'Total Transactions', 
                    value: totalTransactions.toLocaleString(), 
                    color: [59, 130, 246], // Blue
                    bgColor: [239, 246, 255]
                },
                { 
                    label: 'Total Revenue', 
                    value: formatCurrency(totalRevenue), 
                    color: [34, 197, 94], // Green
                    bgColor: [240, 253, 244]
                },
                { 
                    label: 'Total Tax', 
                    value: formatCurrency(totalTax), 
                    color: [249, 115, 22], // Orange
                    bgColor: [255, 247, 237]
                },
                { 
                    label: 'Avg Transaction', 
                    value: formatCurrency(averageTransaction), 
                    color: [139, 92, 246], // Purple
                    bgColor: [245, 243, 255]
                }
            ];

            metrics.forEach((metric, index) => {
                // Start new row every 2 boxes
                if (index > 0 && index % 2 === 0) {
                    boxX = boxesStartX;
                    boxY += boxHeight + boxVerticalSpacing;
                }

                // Box background with subtle color
                doc.setFillColor(metric.bgColor[0], metric.bgColor[1], metric.bgColor[2]);
                doc.rect(boxX, boxY, boxWidth, boxHeight, 'F');
                
                // Left accent border
                doc.setFillColor(metric.color[0], metric.color[1], metric.color[2]);
                doc.rect(boxX, boxY, 3, boxHeight, 'F');
                
                // Box border
                doc.setDrawColor(220, 225, 230);
                doc.setLineWidth(0.4);
                doc.rect(boxX, boxY, boxWidth, boxHeight, 'S');

                // Label with better spacing
                doc.setFontSize(8.5);
                doc.setFont('helvetica', 'normal');
                doc.setTextColor(100, 110, 120);
                const labelX = boxX + 8;
                doc.text(metric.label, labelX, boxY + 8, { maxWidth: boxWidth - 12 });

                // Value with larger, bold font
                doc.setFontSize(12);
                doc.setFont('helvetica', 'bold');
                doc.setTextColor(metric.color[0], metric.color[1], metric.color[2]);
                const valueLines = doc.splitTextToSize(metric.value, boxWidth - 12);
                doc.text(valueLines, labelX, boxY + 17, { maxWidth: boxWidth - 12 });

                boxX += boxWidth + boxSpacing;
            });

            y = boxY + boxHeight + 18;

            // === DETAILED TRANSACTIONS TABLE ===
            doc.setFontSize(13);
            doc.setFont('helvetica', 'bold');
            doc.setTextColor(35, 47, 62);
            doc.text('Detailed Transactions', margin, y);
            y += 10;

            // Get table data
            const rows = [];
            const reportHeaders = ['Period', 'Transactions', 'Subtotal', 'Tax Amount', 'Total Amount'];
            rows.push(reportHeaders);
            
            const bodyRows = table.querySelectorAll('tbody tr');
            bodyRows.forEach(row => {
                const cells = Array.from(row.querySelectorAll('td')).map(td => {
                    let text = td.textContent.trim();
                    // Remove currency symbol and strong tags
                    text = text.replace(/₱/g, '').replace(/<strong>/g, '').replace(/<\/strong>/g, '').trim();
                    return text;
                });
                
                if (cells.length >= 5) {
                    rows.push([
                        cells[0] || '', // Period
                        cells[1] || '', // Transactions
                        cells[2] || '', // Subtotal
                        cells[3] || '', // Tax Amount
                        cells[4] || ''  // Total Amount
                    ]);
                }
            });

            if (rows.length <= 1) {
                doc.setFontSize(10);
                doc.setFont('helvetica', 'normal');
                doc.setTextColor(150, 150, 150);
                doc.text('No transaction data available for the selected period.', margin, y);
                y += 10;
            } else {
                // Table column widths (optimized for 5 columns with better spacing)
                const colWidths = [56, 28, 34, 34, 32]; // Period, Transactions, Subtotal, Tax, Total
                const colAligns = ['left', 'right', 'right', 'right', 'right'];
                
                // Table header with professional styling
                let x = margin;
                const headerY = y;
                
                // Header background with gradient-like effect
                doc.setFillColor(35, 47, 62);
                doc.rect(margin, headerY, contentWidth, 9, 'F');
                
                // Subtle border at bottom of header
                doc.setDrawColor(60, 80, 100);
                doc.setLineWidth(0.3);
                doc.line(margin, headerY + 9, margin + contentWidth, headerY + 9);
                
                // Header text with better spacing
                doc.setFontSize(9.5);
                doc.setFont('helvetica', 'bold');
                doc.setTextColor(255, 255, 255);
                
                rows[0].forEach((header, index) => {
                    const width = colWidths[index];
                    const textX = colAligns[index] === 'right' ? x + width - 4 : x + 4;
                    doc.text(header.toUpperCase(), textX, headerY + 6.5, { 
                        align: colAligns[index],
                        maxWidth: width - 8
                    });
                    x += width;
                });
                
                y = headerY + 10;
                const maxY = pageHeight - margin - 45; // Leave space for footer and summary

                // Table rows with professional alternating colors
                for (let i = 1; i < rows.length; i++) {
                    // Check if we need a new page
                    if (y > maxY) {
                        doc.addPage();
                        y = margin + 10;
                        
                        // Redraw header on new page
                        x = margin;
                        doc.setFillColor(35, 47, 62);
                        doc.rect(margin, y, contentWidth, 9, 'F');
                        
                        doc.setDrawColor(60, 80, 100);
                        doc.setLineWidth(0.3);
                        doc.line(margin, y + 9, margin + contentWidth, y + 9);
                        
                        doc.setFontSize(9.5);
                        doc.setFont('helvetica', 'bold');
                        doc.setTextColor(255, 255, 255);
                        
                        rows[0].forEach((header, index) => {
                            const width = colWidths[index];
                            const textX = colAligns[index] === 'right' ? x + width - 4 : x + 4;
                            doc.text(header.toUpperCase(), textX, y + 6.5, { 
                                align: colAligns[index],
                                maxWidth: width - 8
                            });
                            x += width;
                        });
                        
                        y += 10;
                    }

                    const row = rows[i];
                    const rowY = y;
                    
                    // Alternate row background with subtle color
                    if (i % 2 === 0) {
                        doc.setFillColor(252, 253, 254);
                        doc.rect(margin, rowY, contentWidth, 8, 'F');
                    }
                    
                    x = margin;
                    doc.setFontSize(8.5);
                    
                    row.forEach((cell, index) => {
                        const width = colWidths[index];
                        const cellText = cell || '';
                        const textX = colAligns[index] === 'right' ? x + width - 4 : x + 4;
                        
                        // Enhanced typography for different columns
                        if (index === 4) {
                            // Total amount - bold and prominent
                            doc.setFont('helvetica', 'bold');
                            doc.setTextColor(35, 47, 62);
                            doc.setFontSize(9);
                        } else if (index >= 2) {
                            // Monetary values - semi-bold
                            doc.setFont('helvetica', 'bold');
                            doc.setTextColor(50, 60, 70);
                            doc.setFontSize(8.5);
                        } else if (index === 0) {
                            // Period - medium weight
                            doc.setFont('helvetica', 'normal');
                            doc.setTextColor(50, 60, 70);
                            doc.setFontSize(8.5);
                        } else {
                            // Transactions count
                            doc.setFont('helvetica', 'normal');
                            doc.setTextColor(70, 80, 90);
                            doc.setFontSize(8.5);
                        }
                        
                        doc.text(cellText, textX, rowY + 5.5, { 
                            align: colAligns[index],
                            maxWidth: width - 8
                        });
                        x += width;
                    });
                    
                    // Subtle row separator
                    doc.setDrawColor(240, 242, 245);
                    doc.setLineWidth(0.2);
                    doc.line(margin, rowY + 8, margin + contentWidth, rowY + 8);
                    
                    y += 8;
                }

                // Footer row border - stronger separator
                doc.setDrawColor(200, 205, 210);
                doc.setLineWidth(0.8);
                doc.line(margin, y, margin + contentWidth, y);
            }
            
            // === FOOTER SECTION ===
            const footerY = pageHeight - 18;
            
            // Footer divider with accent
            doc.setFillColor(35, 47, 62);
            doc.rect(margin, footerY, contentWidth, 1, 'F');
            doc.setDrawColor(230, 235, 240);
            doc.setLineWidth(0.3);
            doc.line(margin, footerY + 1, pageWidth - margin, footerY + 1);
            
            // Footer background
            doc.setFillColor(250, 251, 252);
            doc.rect(margin, footerY + 1, contentWidth, 17, 'F');
            
            // Footer text with better formatting
            doc.setFontSize(7.5);
            doc.setFont('helvetica', 'normal');
            doc.setTextColor(120, 130, 140);
            
            const footerText = `Generated on ${issueDate} • Report: ${reportNumber}`;
            doc.text(footerText, margin + 4, footerY + 7);
            
            // Confidentiality notice with subtle styling
            doc.setFontSize(6.5);
            doc.setFont('helvetica', 'italic');
            doc.setTextColor(160, 170, 180);
            doc.text('This report contains confidential information and is intended for authorized personnel only.', 
                pageWidth / 2, footerY + 12, { align: 'center' });
            
            // Company branding in footer
            doc.setFontSize(7);
            doc.setFont('helvetica', 'normal');
            doc.setTextColor(140, 150, 160);
            doc.text('© QUADTECH - All Rights Reserved', pageWidth / 2, footerY + 16, { align: 'center' });
            
            doc.save(filename || 'SalesSummary_Report.pdf');
        } catch (error) {
            console.error('Error generating PDF:', error);
            alert('Error generating PDF: ' + error.message);
        }
    },

    exportInventoryReportToPdf: function (tableId, reportNumber, issueDate, categoryFilter, brandFilter, totalProducts, totalQuantity, totalStockValue, lowStockItems, allReportDataJson, filename) {
        // Check if jsPDF is already loaded
        let jsPDF;
        if (typeof window.jspdf !== 'undefined' && window.jspdf.jsPDF) {
            jsPDF = window.jspdf.jsPDF;
        } else if (typeof window.jspdf !== 'undefined') {
            jsPDF = window.jspdf;
        } else {
            console.error('jsPDF library not loaded.');
            alert('PDF export failed: jsPDF library not loaded. Please refresh the page and try again.');
            return;
        }

        try {
            const doc = new jsPDF('l', 'mm', 'a4'); // Landscape for wider table
            
            const table = document.getElementById(tableId);
            if (!table) {
                console.error('Table not found:', tableId);
                alert('Table not found. Please ensure the report table is visible.');
                return;
            }

            // Page dimensions with professional margins (landscape)
            const pageWidth = 297;
            const pageHeight = 210;
            const margin = 12;
            const contentWidth = pageWidth - (margin * 2);
            let y = margin;

            // Helper function to format numbers with currency
            const formatCurrency = (num) => {
                const value = parseFloat(num) || 0;
                return '₱' + value.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
            };

            const formatNumber = (num) => {
                const value = parseFloat(num) || 0;
                return value.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
            };

            // === HEADER SECTION ===
            // Header background bar
            doc.setFillColor(41, 53, 65); // Dark blue-gray
            doc.rect(0, 0, pageWidth, 35, 'F');
            
            // Company name/Logo area (left side of header)
            doc.setFontSize(18);
            doc.setFont('helvetica', 'bold');
            doc.setTextColor(255, 255, 255);
            doc.text('QUADTECH', margin, 18);
            
            // Report title (in header)
            doc.setFontSize(16);
            doc.setTextColor(255, 255, 255);
            doc.text('Inventory Report', pageWidth / 2, 20, { align: 'center' });
            
            y = 40; // Start content below header

            // === REPORT METADATA SECTION ===
            doc.setFontSize(8);
            doc.setFont('helvetica', 'normal');
            doc.setTextColor(100, 100, 100);
            
            // Report details in three columns
            const metadataCol1X = margin;
            const metadataCol2X = pageWidth / 3;
            const metadataCol3X = (pageWidth / 3) * 2;
            let metadataY = y;
            
            doc.text(`Report Number: ${reportNumber}`, metadataCol1X, metadataY);
            doc.text(`Report Date: ${issueDate}`, metadataCol2X, metadataY);
            metadataY += 4;
            
            doc.text(`Category: ${categoryFilter}`, metadataCol1X, metadataY);
            doc.text(`Brand: ${brandFilter}`, metadataCol2X, metadataY);
            
            y = metadataY + 8;

            // Divider line
            doc.setDrawColor(220, 220, 220);
            doc.setLineWidth(0.5);
            doc.line(margin, y, pageWidth - margin, y);
            y += 6;

            // === EXECUTIVE SUMMARY SECTION ===
            doc.setFontSize(11);
            doc.setFont('helvetica', 'bold');
            doc.setTextColor(41, 53, 65);
            doc.text('Executive Summary', margin, y);
            y += 7;

            // Summary boxes with borders (4 boxes in a row for landscape)
            const boxWidth = (contentWidth - 15) / 4; // 4 boxes with spacing
            const boxHeight = 18;
            const boxSpacing = 5;
            let boxX = margin;
            const boxY = y;

            // Summary metrics from parameters
            const metrics = [
                { label: 'Total Products', value: totalProducts.toLocaleString(), icon: '📦' },
                { label: 'Total Quantity', value: totalQuantity.toLocaleString(), icon: '📊' },
                { label: 'Total Stock Value', value: formatCurrency(totalStockValue), icon: '💰' },
                { label: 'Low Stock Items', value: lowStockItems.toLocaleString(), icon: '⚠️', highlight: lowStockItems > 0 }
            ];

            metrics.forEach((metric, index) => {
                // Box background (light gray, or red for low stock if applicable)
                if (metric.highlight) {
                    doc.setFillColor(255, 245, 245); // Light red background
                } else {
                    doc.setFillColor(245, 247, 250);
                }
                doc.rect(boxX, boxY, boxWidth, boxHeight, 'F');
                
                // Box border (red for low stock warning)
                doc.setDrawColor(metric.highlight ? 220 : 200, metric.highlight ? 50 : 200, metric.highlight ? 50 : 200);
                doc.setLineWidth(metric.highlight ? 0.5 : 0.3);
                doc.rect(boxX, boxY, boxWidth, boxHeight, 'S');

                // Label
                doc.setFontSize(7);
                doc.setFont('helvetica', 'normal');
                doc.setTextColor(100, 100, 100);
                const labelLines = doc.splitTextToSize(metric.label, boxWidth - 4);
                doc.text(labelLines, boxX + boxWidth / 2, boxY + 5, { align: 'center', maxWidth: boxWidth - 4 });

                // Value
                doc.setFontSize(9);
                doc.setFont('helvetica', 'bold');
                doc.setTextColor(metric.highlight ? 200 : 41, metric.highlight ? 50 : 53, metric.highlight ? 50 : 65);
                const valueLines = doc.splitTextToSize(metric.value, boxWidth - 4);
                doc.text(valueLines, boxX + boxWidth / 2, boxY + 12, { align: 'center', maxWidth: boxWidth - 4 });

                boxX += boxWidth + boxSpacing;
            });

            y = boxY + boxHeight + 12;

            // === DETAILED INVENTORY TABLE ===
            doc.setFontSize(11);
            doc.setFont('helvetica', 'bold');
            doc.setTextColor(41, 53, 65);
            doc.text('Detailed Inventory', margin, y);
            y += 7;

            // Get table data from JSON (all filtered data, not just visible paginated rows)
            const rows = [];
            const reportHeaders = ['Product Name', 'SKU', 'Category', 'Brand', 'Quantity', 'Cost Price', 'Sell Price', 'Stock Value', 'Status'];
            rows.push(reportHeaders);
            
            // Parse JSON data if provided, otherwise fall back to table
            let allData = [];
            if (allReportDataJson && typeof allReportDataJson === 'string' && allReportDataJson.trim() !== '') {
                try {
                    allData = JSON.parse(allReportDataJson);
                } catch (e) {
                    console.warn('Failed to parse JSON data, falling back to table rows:', e);
                    allData = [];
                }
            }
            
            if (allData.length > 0) {
                // Use JSON data (all filtered rows)
                allData.forEach(item => {
                    rows.push([
                        item.ProductName || '',
                        item.SKU || '',
                        item.Category || '',
                        item.Brand || '',
                        item.Quantity?.toString() || '0',
                        item.CostPrice || '0.00',
                        item.SellPrice || '0.00',
                        item.StockValue || '0.00',
                        item.Status || ''
                    ]);
                });
            } else {
                // Fallback: Get data from visible table rows (for backward compatibility)
                const bodyRows = table.querySelectorAll('tbody tr');
                bodyRows.forEach(row => {
                    const cells = Array.from(row.querySelectorAll('td')).map(td => {
                        let text = td.textContent.trim();
                        // Remove currency symbol, strong tags, and status badge classes
                        text = text.replace(/₱/g, '')
                                  .replace(/<strong>/g, '')
                                  .replace(/<\/strong>/g, '')
                                  .replace(/<span[^>]*>/g, '')
                                  .replace(/<\/span>/g, '')
                                  .trim();
                        return text;
                    });
                    
                    if (cells.length >= 9) {
                        rows.push([
                            cells[0] || '', // Product Name
                            cells[1] || '', // SKU
                            cells[2] || '', // Category
                            cells[3] || '', // Brand
                            cells[4] || '', // Quantity
                            cells[5] || '', // Cost Price
                            cells[6] || '', // Sell Price
                            cells[7] || '', // Stock Value
                            cells[8] || ''  // Status
                        ]);
                    }
                });
            }

            if (rows.length <= 1) {
                doc.setFontSize(9);
                doc.setFont('helvetica', 'normal');
                doc.setTextColor(150, 150, 150);
                doc.text('No inventory data available for the selected filters.', margin, y);
                y += 10;
            } else {
                // Table column widths (optimized for 9 columns in landscape)
                const colWidths = [45, 25, 28, 28, 18, 22, 22, 25, 18]; // Adjusted for landscape
                const colAligns = ['left', 'left', 'left', 'left', 'right', 'right', 'right', 'right', 'center'];
                
                // Table header with background
                let x = margin;
                const headerY = y;
                
                // Header background
                doc.setFillColor(41, 53, 65);
                doc.rect(margin, headerY, contentWidth, 7, 'F');
                
                // Header text
                doc.setFontSize(7);
                doc.setFont('helvetica', 'bold');
                doc.setTextColor(255, 255, 255);
                
                rows[0].forEach((header, index) => {
                    const width = colWidths[index];
                    const textX = colAligns[index] === 'right' ? x + width - 2 : 
                                 colAligns[index] === 'center' ? x + width / 2 : x + 2;
                    doc.text(header, textX, headerY + 5, { 
                        align: colAligns[index],
                        maxWidth: width - 4
                    });
                    x += width;
                });
                
                y = headerY + 8;
                const maxY = pageHeight - margin - 25; // Leave space for footer and summary

                // Table rows with alternating colors
                for (let i = 1; i < rows.length; i++) {
                    // Check if we need a new page
                    if (y > maxY) {
                        doc.addPage('l'); // Landscape
                        y = margin + 10;
                        
                        // Redraw header on new page
                        x = margin;
                        doc.setFillColor(41, 53, 65);
                        doc.rect(margin, y, contentWidth, 7, 'F');
                        
                        doc.setFontSize(7);
                        doc.setFont('helvetica', 'bold');
                        doc.setTextColor(255, 255, 255);
                        
                        rows[0].forEach((header, index) => {
                            const width = colWidths[index];
                            const textX = colAligns[index] === 'right' ? x + width - 2 : 
                                         colAligns[index] === 'center' ? x + width / 2 : x + 2;
                            doc.text(header, textX, y + 5, { 
                                align: colAligns[index],
                                maxWidth: width - 4
                            });
                            x += width;
                        });
                        
                        y += 8;
                    }

                    const row = rows[i];
                    const rowY = y;
                    const quantity = parseFloat(row[4]?.replace(/,/g, '') || 0);
                    const isLowStock = quantity <= 5;
                    
                    // Alternate row background, with special highlight for low stock
                    if (isLowStock) {
                        doc.setFillColor(255, 245, 245); // Light red for low stock
                        doc.rect(margin, rowY, contentWidth, 6, 'F');
                    } else if (i % 2 === 0) {
                        doc.setFillColor(250, 250, 250);
                        doc.rect(margin, rowY, contentWidth, 6, 'F');
                    }
                    
                    x = margin;
                    doc.setFontSize(7);
                    doc.setFont('helvetica', 'normal');
                    doc.setTextColor(isLowStock ? 200 : 60, isLowStock ? 50 : 60, isLowStock ? 50 : 60);
                    
                    row.forEach((cell, index) => {
                        const width = colWidths[index];
                        const cellText = cell || '';
                        const textX = colAligns[index] === 'right' ? x + width - 2 : 
                                     colAligns[index] === 'center' ? x + width / 2 : x + 2;
                        
                        // Make monetary values and stock value bold
                        if (index >= 5 && index <= 7) {
                            doc.setFont('helvetica', 'bold');
                            doc.setTextColor(isLowStock ? 200 : 41, isLowStock ? 50 : 53, isLowStock ? 50 : 65);
                        } else if (index === 4) {
                            // Quantity - bold if low stock
                            if (isLowStock) {
                                doc.setFont('helvetica', 'bold');
                            }
                        } else {
                            doc.setFont('helvetica', 'normal');
                            doc.setTextColor(isLowStock ? 200 : 60, isLowStock ? 50 : 60, isLowStock ? 50 : 60);
                        }
                        
                        doc.text(cellText, textX, rowY + 4, { 
                            align: colAligns[index],
                            maxWidth: width - 4
                        });
                        x += width;
                    });
                    
                    // Row border
                    doc.setDrawColor(230, 230, 230);
                    doc.setLineWidth(0.2);
                    doc.line(margin, rowY + 6, margin + contentWidth, rowY + 6);
                    
                    y += 6;
                }

                // Footer row border
                doc.setDrawColor(180, 180, 180);
                doc.setLineWidth(0.5);
                doc.line(margin, y, margin + contentWidth, y);
            }
            
            // === FOOTER SECTION ===
            const footerY = pageHeight - 12;
            
            // Footer divider
            doc.setDrawColor(220, 220, 220);
            doc.setLineWidth(0.5);
            doc.line(margin, footerY, pageWidth - margin, footerY);
            
            // Footer text
            doc.setFontSize(7);
            doc.setFont('helvetica', 'normal');
            doc.setTextColor(150, 150, 150);
            
            const footerText = `Generated on ${issueDate} | Report: ${reportNumber}`;
            doc.text(footerText, margin, footerY + 4);
            
            // Confidentiality notice
            doc.setFontSize(6);
            doc.setTextColor(180, 180, 180);
            doc.text('This report contains confidential information and is intended for authorized personnel only.', 
                pageWidth / 2, footerY + 8, { align: 'center' });
            
            doc.save(filename || 'InventoryReport.pdf');
        } catch (error) {
            console.error('Error generating PDF:', error);
            alert('Error generating PDF: ' + error.message);
        }
    },

    exportPurchaseOrderReportToPdf: function (tableId, reportNumber, issueDate, startDate, endDate, supplierFilter, statusFilter, totalOrders, totalItems, totalAmount, pendingOrders, allReportDataJson, filename) {
        // Check if jsPDF is already loaded
        let jsPDF;
        if (typeof window.jspdf !== 'undefined' && window.jspdf.jsPDF) {
            jsPDF = window.jspdf.jsPDF;
        } else if (typeof window.jspdf !== 'undefined') {
            jsPDF = window.jspdf;
        } else {
            console.error('jsPDF library not loaded.');
            alert('PDF export failed: jsPDF library not loaded. Please refresh the page and try again.');
            return;
        }

        try {
            const doc = new jsPDF('l', 'mm', 'a4'); // Landscape for wider table
            
            const table = document.getElementById(tableId);
            if (!table) {
                console.error('Table not found:', tableId);
                alert('Table not found. Please ensure the report table is visible.');
                return;
            }

            // Page dimensions with professional margins (landscape)
            const pageWidth = 297;
            const pageHeight = 210;
            const margin = 12;
            const contentWidth = pageWidth - (margin * 2);
            let y = margin;

            // Helper function to format numbers with currency
            const formatCurrency = (num) => {
                const value = parseFloat(num) || 0;
                return '₱' + value.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
            };

            const formatNumber = (num) => {
                const value = parseFloat(num) || 0;
                return value.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
            };

            // === HEADER SECTION ===
            // Header background bar
            doc.setFillColor(41, 53, 65); // Dark blue-gray
            doc.rect(0, 0, pageWidth, 35, 'F');
            
            // Company name/Logo area (left side of header)
            doc.setFontSize(18);
            doc.setFont('helvetica', 'bold');
            doc.setTextColor(255, 255, 255);
            doc.text('QUADTECH', margin, 18);
            
            // Report title (in header)
            doc.setFontSize(16);
            doc.setTextColor(255, 255, 255);
            doc.text('Purchase Order Report', pageWidth / 2, 20, { align: 'center' });
            
            y = 40; // Start content below header

            // === REPORT METADATA SECTION ===
            doc.setFontSize(8);
            doc.setFont('helvetica', 'normal');
            doc.setTextColor(100, 100, 100);
            
            // Report details in three columns
            const metadataCol1X = margin;
            const metadataCol2X = pageWidth / 3;
            const metadataCol3X = (pageWidth / 3) * 2;
            let metadataY = y;
            
            doc.text(`Report Number: ${reportNumber}`, metadataCol1X, metadataY);
            doc.text(`Report Date: ${issueDate}`, metadataCol2X, metadataY);
            metadataY += 4;
            
            doc.text(`Period: ${startDate} - ${endDate}`, metadataCol1X, metadataY);
            doc.text(`Supplier: ${supplierFilter}`, metadataCol2X, metadataY);
            doc.text(`Status: ${statusFilter}`, metadataCol3X, metadataY);
            
            y = metadataY + 8;

            // Divider line
            doc.setDrawColor(220, 220, 220);
            doc.setLineWidth(0.5);
            doc.line(margin, y, pageWidth - margin, y);
            y += 6;

            // === EXECUTIVE SUMMARY SECTION ===
            doc.setFontSize(11);
            doc.setFont('helvetica', 'bold');
            doc.setTextColor(41, 53, 65);
            doc.text('Executive Summary', margin, y);
            y += 7;

            // Summary boxes with borders (4 boxes in a row for landscape)
            const boxWidth = (contentWidth - 15) / 4; // 4 boxes with spacing
            const boxHeight = 18;
            const boxSpacing = 5;
            let boxX = margin;
            const boxY = y;

            // Summary metrics from parameters
            const metrics = [
                { label: 'Total Orders', value: totalOrders.toLocaleString(), icon: '📋' },
                { label: 'Total Items', value: totalItems.toLocaleString(), icon: '📦' },
                { label: 'Total Amount', value: formatCurrency(totalAmount), icon: '💰' },
                { label: 'Pending Orders', value: pendingOrders.toLocaleString(), icon: '⏳', highlight: pendingOrders > 0 }
            ];

            metrics.forEach((metric, index) => {
                // Box background (light gray, or yellow/orange for pending orders if applicable)
                if (metric.highlight) {
                    doc.setFillColor(255, 250, 230); // Light yellow background
                } else {
                    doc.setFillColor(245, 247, 250);
                }
                doc.rect(boxX, boxY, boxWidth, boxHeight, 'F');
                
                // Box border (orange for pending orders warning)
                doc.setDrawColor(metric.highlight ? 255 : 200, metric.highlight ? 180 : 200, metric.highlight ? 50 : 200);
                doc.setLineWidth(metric.highlight ? 0.5 : 0.3);
                doc.rect(boxX, boxY, boxWidth, boxHeight, 'S');

                // Label
                doc.setFontSize(7);
                doc.setFont('helvetica', 'normal');
                doc.setTextColor(100, 100, 100);
                const labelLines = doc.splitTextToSize(metric.label, boxWidth - 4);
                doc.text(labelLines, boxX + boxWidth / 2, boxY + 5, { align: 'center', maxWidth: boxWidth - 4 });

                // Value
                doc.setFontSize(9);
                doc.setFont('helvetica', 'bold');
                doc.setTextColor(metric.highlight ? 200 : 41, metric.highlight ? 120 : 53, metric.highlight ? 0 : 65);
                const valueLines = doc.splitTextToSize(metric.value, boxWidth - 4);
                doc.text(valueLines, boxX + boxWidth / 2, boxY + 12, { align: 'center', maxWidth: boxWidth - 4 });

                boxX += boxWidth + boxSpacing;
            });

            y = boxY + boxHeight + 12;

            // === DETAILED PURCHASE ORDERS TABLE ===
            doc.setFontSize(11);
            doc.setFont('helvetica', 'bold');
            doc.setTextColor(41, 53, 65);
            doc.text('Purchase Order Details', margin, y);
            y += 7;

            // Get table data from JSON (all filtered data, not just visible paginated rows)
            const rows = [];
            const reportHeaders = ['PO Number', 'Order Date', 'Expected Delivery', 'Supplier', 'Status', 'Item Count', 'Total Amount'];
            rows.push(reportHeaders);
            
            // Parse JSON data if provided, otherwise fall back to table
            let allData = [];
            if (allReportDataJson && typeof allReportDataJson === 'string' && allReportDataJson.trim() !== '') {
                try {
                    allData = JSON.parse(allReportDataJson);
                } catch (e) {
                    console.warn('Failed to parse JSON data, falling back to table rows:', e);
                    allData = [];
                }
            }
            
            if (allData.length > 0) {
                // Use JSON data (all filtered rows)
                allData.forEach(item => {
                    rows.push([
                        item.PurchaseOrderNumber || '',
                        item.OrderDate || '',
                        item.ExpectedDeliveryDate || 'N/A',
                        item.SupplierName || '',
                        item.Status || '',
                        item.ItemCount?.toString() || '0',
                        item.TotalAmount || '0.00'
                    ]);
                });
            } else {
                // Fallback: Get data from visible table rows (for backward compatibility)
                const bodyRows = table.querySelectorAll('tbody tr');
                bodyRows.forEach(row => {
                    const cells = Array.from(row.querySelectorAll('td')).map(td => {
                        let text = td.textContent.trim();
                        // Remove currency symbol, strong tags, and status badge classes
                        text = text.replace(/₱/g, '')
                                  .replace(/<strong>/g, '')
                                  .replace(/<\/strong>/g, '')
                                  .replace(/<span[^>]*>/g, '')
                                  .replace(/<\/span>/g, '')
                                  .trim();
                        return text;
                    });
                    
                    if (cells.length >= 7) {
                        rows.push([
                            cells[0] || '', // PO Number
                            cells[1] || '', // Order Date
                            cells[2] || '', // Expected Delivery
                            cells[3] || '', // Supplier
                            cells[4] || '', // Status
                            cells[5] || '', // Item Count
                            cells[6] || ''  // Total Amount
                        ]);
                    }
                });
            }

            if (rows.length <= 1) {
                doc.setFontSize(9);
                doc.setFont('helvetica', 'normal');
                doc.setTextColor(150, 150, 150);
                doc.text('No purchase order data available for the selected filters.', margin, y);
                y += 10;
            } else {
                // Table column widths (optimized for 7 columns in landscape)
                const colWidths = [30, 25, 25, 45, 22, 18, 30]; // Adjusted for landscape
                const colAligns = ['left', 'center', 'center', 'left', 'center', 'right', 'right'];
                
                // Table header with background
                let x = margin;
                const headerY = y;
                
                // Header background
                doc.setFillColor(41, 53, 65);
                doc.rect(margin, headerY, contentWidth, 7, 'F');
                
                // Header text
                doc.setFontSize(7);
                doc.setFont('helvetica', 'bold');
                doc.setTextColor(255, 255, 255);
                
                rows[0].forEach((header, index) => {
                    const width = colWidths[index];
                    const textX = colAligns[index] === 'right' ? x + width - 2 : 
                                 colAligns[index] === 'center' ? x + width / 2 : x + 2;
                    doc.text(header, textX, headerY + 5, { 
                        align: colAligns[index],
                        maxWidth: width - 4
                    });
                    x += width;
                });
                
                y = headerY + 8;
                const maxY = pageHeight - margin - 25; // Leave space for footer and summary

                // Table rows with alternating colors and status highlighting
                for (let i = 1; i < rows.length; i++) {
                    // Check if we need a new page
                    if (y > maxY) {
                        doc.addPage('l'); // Landscape
                        y = margin + 10;
                        
                        // Redraw header on new page
                        x = margin;
                        doc.setFillColor(41, 53, 65);
                        doc.rect(margin, y, contentWidth, 7, 'F');
                        
                        doc.setFontSize(7);
                        doc.setFont('helvetica', 'bold');
                        doc.setTextColor(255, 255, 255);
                        
                        rows[0].forEach((header, index) => {
                            const width = colWidths[index];
                            const textX = colAligns[index] === 'right' ? x + width - 2 : 
                                         colAligns[index] === 'center' ? x + width / 2 : x + 2;
                            doc.text(header, textX, y + 5, { 
                                align: colAligns[index],
                                maxWidth: width - 4
                            });
                            x += width;
                        });
                        
                        y += 8;
                    }

                    const row = rows[i];
                    const rowY = y;
                    const status = (row[4] || '').toLowerCase();
                    const isPending = status === 'pending';
                    const isCancelled = status === 'cancelled';
                    const isDelivered = status === 'delivered';
                    
                    // Row background based on status
                    if (isCancelled) {
                        doc.setFillColor(250, 245, 245); // Light red for cancelled
                    } else if (isPending) {
                        doc.setFillColor(255, 250, 230); // Light yellow for pending
                    } else if (i % 2 === 0) {
                        doc.setFillColor(250, 250, 250); // Light gray for alternate rows
                    }
                    
                    if (isCancelled || isPending || (i % 2 === 0 && !isDelivered)) {
                        doc.rect(margin, rowY, contentWidth, 6, 'F');
                    }
                    
                    x = margin;
                    doc.setFontSize(7);
                    doc.setFont('helvetica', 'normal');
                    
                    // Set text color based on status
                    if (isCancelled) {
                        doc.setTextColor(200, 50, 50);
                    } else if (isPending) {
                        doc.setTextColor(200, 140, 0);
                    } else {
                        doc.setTextColor(60, 60, 60);
                    }
                    
                    row.forEach((cell, index) => {
                        const width = colWidths[index];
                        const cellText = cell || '';
                        const textX = colAligns[index] === 'right' ? x + width - 2 : 
                                     colAligns[index] === 'center' ? x + width / 2 : x + 2;
                        
                        // Make monetary values and PO number bold
                        if (index === 6) { // Total Amount
                            doc.setFont('helvetica', 'bold');
                            if (isCancelled) {
                                doc.setTextColor(200, 50, 50);
                            } else if (isPending) {
                                doc.setTextColor(200, 140, 0);
                            } else {
                                doc.setTextColor(41, 53, 65);
                            }
                        } else if (index === 0) { // PO Number
                            doc.setFont('helvetica', 'bold');
                        } else {
                            doc.setFont('helvetica', 'normal');
                        }
                        
                        doc.text(cellText, textX, rowY + 4, { 
                            align: colAligns[index],
                            maxWidth: width - 4
                        });
                        x += width;
                    });
                    
                    // Row border
                    doc.setDrawColor(230, 230, 230);
                    doc.setLineWidth(0.2);
                    doc.line(margin, rowY + 6, margin + contentWidth, rowY + 6);
                    
                    y += 6;
                }

                // Footer row border
                doc.setDrawColor(180, 180, 180);
                doc.setLineWidth(0.5);
                doc.line(margin, y, margin + contentWidth, y);
            }
            
            // === FOOTER SECTION ===
            const footerY = pageHeight - 12;
            
            // Footer divider
            doc.setDrawColor(220, 220, 220);
            doc.setLineWidth(0.5);
            doc.line(margin, footerY, pageWidth - margin, footerY);
            
            // Footer text
            doc.setFontSize(7);
            doc.setFont('helvetica', 'normal');
            doc.setTextColor(150, 150, 150);
            
            const footerText = `Generated on ${issueDate} | Report: ${reportNumber}`;
            doc.text(footerText, margin, footerY + 4);
            
            // Confidentiality notice
            doc.setFontSize(6);
            doc.setTextColor(180, 180, 180);
            doc.text('This report contains confidential information and is intended for authorized personnel only.', 
                pageWidth / 2, footerY + 8, { align: 'center' });
            
            doc.save(filename || 'PurchaseOrderReport.pdf');
        } catch (error) {
            console.error('Error generating PDF:', error);
            alert('Error generating PDF: ' + error.message);
        }
    }
};
