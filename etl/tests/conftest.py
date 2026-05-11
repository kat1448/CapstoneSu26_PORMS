import sys
import os

# Thêm thư mục etl/ vào sys.path để pytest tìm được các modules
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
