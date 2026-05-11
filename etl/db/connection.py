from contextlib import contextmanager
from sqlalchemy import create_engine, text
from sqlalchemy.orm import sessionmaker
from sqlalchemy.pool import NullPool

from config import settings

# Operational DB (porms_etl role)
_operational_engine = create_engine(
    settings.POSTGRES_DSN,
    poolclass=NullPool,
    connect_args={"options": "-c search_path=operational,public"},
    echo=False,
)
OperationalSession = sessionmaker(bind=_operational_engine)

# Analytics DB (same DSN, different search_path)
_analytics_engine = create_engine(
    settings.POSTGRES_DSN,
    poolclass=NullPool,
    connect_args={"options": "-c search_path=analytics,public"},
    echo=False,
)
AnalyticsSession = sessionmaker(bind=_analytics_engine)


@contextmanager
def get_operational_session():
    session = OperationalSession()
    try:
        yield session
        session.commit()
    except Exception:
        session.rollback()
        raise
    finally:
        session.close()


@contextmanager
def get_analytics_session():
    session = AnalyticsSession()
    try:
        yield session
        session.commit()
    except Exception:
        session.rollback()
        raise
    finally:
        session.close()
